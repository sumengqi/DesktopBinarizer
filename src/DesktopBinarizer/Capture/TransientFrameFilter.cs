using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace DesktopBinarizer.Capture;

public sealed class TransientFrameFilter : IDisposable
{
    private const double LargeChangeRatio = 0.18;
    private const double LargeMeanDifference = 20;
    private const double SimilarChangeRatio = 0.12;
    private const double SimilarMeanDifference = 14;

    private readonly object _sync = new();
    private readonly Dictionary<nint, FrameState> _states = [];
    private bool _disposed;

    public Bitmap Stabilize(nint key, Bitmap current)
    {
        ArgumentNullException.ThrowIfNull(current);

        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_states.TryGetValue(key, out FrameState? state) ||
                state.Stable.Size != current.Size)
            {
                state?.Dispose();
                _states[key] = new FrameState(new Bitmap(current));
                return new Bitmap(current);
            }

            DifferenceMetrics stableDifference = Compare(state.Stable, current);
            if (!IsLargeChange(stableDifference))
            {
                state.ReplaceStable(current);
                state.ClearPending();
                return new Bitmap(current);
            }

            if (state.Pending is not null)
            {
                DifferenceMetrics pendingDifference = Compare(state.Pending, current);
                if (IsSimilar(pendingDifference))
                {
                    state.ReplaceStable(current);
                    state.ClearPending();
                    return new Bitmap(current);
                }
            }

            state.ReplacePending(current);
            return new Bitmap(state.Stable);
        }
    }

    public void Remove(nint key)
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_states.Remove(key, out FrameState? state))
            {
                state.Dispose();
            }
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            foreach (FrameState state in _states.Values)
            {
                state.Dispose();
            }

            _states.Clear();
            _disposed = true;
        }

        GC.SuppressFinalize(this);
    }

    private static bool IsLargeChange(DifferenceMetrics difference)
    {
        return difference.ChangedRatio >= LargeChangeRatio &&
            difference.MeanDifference >= LargeMeanDifference;
    }

    private static bool IsSimilar(DifferenceMetrics difference)
    {
        return difference.ChangedRatio <= SimilarChangeRatio ||
            difference.MeanDifference <= SimilarMeanDifference;
    }

    private static DifferenceMetrics Compare(Bitmap first, Bitmap second)
    {
        Rectangle bounds = new(0, 0, first.Width, first.Height);
        BitmapData? firstData = null;
        BitmapData? secondData = null;
        try
        {
            firstData = first.LockBits(
                bounds,
                ImageLockMode.ReadOnly,
                PixelFormat.Format32bppArgb);
            secondData = second.LockBits(
                bounds,
                ImageLockMode.ReadOnly,
                PixelFormat.Format32bppArgb);

            int stepX = Math.Max(1, first.Width / 80);
            int stepY = Math.Max(1, first.Height / 60);
            int rowByteCount = first.Width * 4;
            byte[] firstRowPixels = new byte[rowByteCount];
            byte[] secondRowPixels = new byte[rowByteCount];
            long differenceTotal = 0;
            int changedCount = 0;
            int sampleCount = 0;
            for (int y = 0; y < first.Height; y += stepY)
            {
                Marshal.Copy(
                    firstData.Scan0 + (y * firstData.Stride),
                    firstRowPixels,
                    0,
                    rowByteCount);
                Marshal.Copy(
                    secondData.Scan0 + (y * secondData.Stride),
                    secondRowPixels,
                    0,
                    rowByteCount);
                for (int x = 0; x < first.Width; x += stepX)
                {
                    int offset = x * 4;
                    int difference =
                        (Math.Abs(firstRowPixels[offset] -
                            secondRowPixels[offset]) +
                        Math.Abs(firstRowPixels[offset + 1] -
                            secondRowPixels[offset + 1]) +
                        Math.Abs(firstRowPixels[offset + 2] -
                            secondRowPixels[offset + 2])) / 3;
                    differenceTotal += difference;
                    changedCount += difference >= 60 ? 1 : 0;
                    sampleCount++;
                }
            }

            return new DifferenceMetrics(
                differenceTotal / (double)sampleCount,
                changedCount / (double)sampleCount);
        }
        finally
        {
            if (firstData is not null)
            {
                first.UnlockBits(firstData);
            }

            if (secondData is not null)
            {
                second.UnlockBits(secondData);
            }
        }
    }

    private sealed class FrameState(Bitmap stable) : IDisposable
    {
        public Bitmap Stable { get; private set; } = stable;

        public Bitmap? Pending { get; private set; }

        public void ReplaceStable(Bitmap bitmap)
        {
            Stable.Dispose();
            Stable = new Bitmap(bitmap);
        }

        public void ReplacePending(Bitmap bitmap)
        {
            Pending?.Dispose();
            Pending = new Bitmap(bitmap);
        }

        public void ClearPending()
        {
            Pending?.Dispose();
            Pending = null;
        }

        public void Dispose()
        {
            Stable.Dispose();
            Pending?.Dispose();
        }
    }

    private readonly record struct DifferenceMetrics(
        double MeanDifference,
        double ChangedRatio);
}
