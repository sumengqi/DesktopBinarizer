using System.Diagnostics;
using System.Drawing.Imaging;

namespace DesktopBinarizer.Capture;

public sealed class ScreenCaptureService : IDisposable
{
    private const long DesktopFilterKeyMask = 1L << 62;
    private readonly TransientFrameFilter _frameFilter = new();

    public IReadOnlyList<WindowInfo> GetCapturableWindows()
    {
        List<WindowInfo> windows = [];
        int currentProcessId = Environment.ProcessId;

        NativeMethods.EnumWindows((handle, _) =>
        {
            if (!NativeMethods.IsWindowVisible(handle))
            {
                return true;
            }

            int titleLength = NativeMethods.GetWindowTextLengthW(handle);
            if (titleLength <= 0)
            {
                return true;
            }

            NativeMethods.GetWindowThreadProcessId(handle, out uint processId);
            if (processId == currentProcessId)
            {
                return true;
            }

            NativeMethods.GetWindowRect(handle, out NativeMethods.Rect rect);
            if (rect.Width < 2 || rect.Height < 2)
            {
                return true;
            }

            var title = new System.Text.StringBuilder(titleLength + 1);
            NativeMethods.GetWindowTextW(handle, title, title.Capacity);
            string windowTitle = title.ToString().Trim();
            if (windowTitle.Length == 0)
            {
                return true;
            }

            string processName = string.Empty;
            try
            {
                using Process process = Process.GetProcessById((int)processId);
                processName = process.ProcessName;
            }
            catch (ArgumentException)
            {
                // The process may exit while the window list is being built.
            }

            windows.Add(new WindowInfo(handle, windowTitle, processName));
            return true;
        }, nint.Zero);

        return windows
            .OrderBy(window => window.ProcessName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(window => window.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    public Bitmap CaptureDesktop()
    {
        Rectangle bounds = SystemInformation.VirtualScreen;
        IReadOnlyList<(nint Handle, Rectangle Bounds)> stabilizedWindows =
            GetStabilizedWindows();
        if (stabilizedWindows.Count > 0)
        {
            WaitForDesktopComposition();
        }

        Bitmap desktop = CaptureScreenRegion(bounds);
        try
        {
            foreach ((nint handle, Rectangle windowBounds) in stabilizedWindows)
            {
                Rectangle visibleBounds = Rectangle.Intersect(bounds, windowBounds);
                if (visibleBounds.Width < 2 || visibleBounds.Height < 2)
                {
                    continue;
                }

                var sourceBounds = new Rectangle(
                    visibleBounds.Left - bounds.Left,
                    visibleBounds.Top - bounds.Top,
                    visibleBounds.Width,
                    visibleBounds.Height);
                using Bitmap currentRegion = desktop.Clone(
                    sourceBounds,
                    PixelFormat.Format32bppArgb);
                using Bitmap stableRegion =
                    _frameFilter.Stabilize(
                        GetDesktopFilterKey(handle),
                        currentRegion);
                using Graphics graphics = Graphics.FromImage(desktop);
                graphics.DrawImageUnscaled(
                    stableRegion,
                    sourceBounds.Location);
            }

            return desktop;
        }
        catch
        {
            desktop.Dispose();
            throw;
        }
    }

    public Bitmap CaptureWindow(nint handle)
    {
        if (!NativeMethods.GetWindowRect(handle, out NativeMethods.Rect rect) ||
            rect.Width <= 0 ||
            rect.Height <= 0)
        {
            throw new InvalidOperationException("目标窗口已关闭或尺寸无效。");
        }

        var bitmap = new Bitmap(rect.Width, rect.Height, PixelFormat.Format32bppArgb);
        bool printSucceeded;

        using (Graphics graphics = Graphics.FromImage(bitmap))
        {
            nint hdc = graphics.GetHdc();
            try
            {
                printSucceeded = NativeMethods.PrintWindow(
                    handle,
                    hdc,
                    NativeMethods.PwRenderFullContent);
            }
            finally
            {
                graphics.ReleaseHdc(hdc);
            }
        }

        if (printSucceeded)
        {
            if (RequiresTransientFrameFiltering(handle))
            {
                using (bitmap)
                {
                    return _frameFilter.Stabilize(handle, bitmap);
                }
            }

            return bitmap;
        }

        WaitForDesktopComposition();
        bitmap.Dispose();
        Bitmap screenCapture = CaptureScreenRegion(
            new Rectangle(rect.Left, rect.Top, rect.Width, rect.Height));
        if (!RequiresTransientFrameFiltering(handle))
        {
            return screenCapture;
        }

        using (screenCapture)
        {
            return _frameFilter.Stabilize(handle, screenCapture);
        }
    }

    private static Bitmap CaptureScreenRegion(Rectangle bounds)
    {
        var bitmap = new Bitmap(
            bounds.Width,
            bounds.Height,
            PixelFormat.Format32bppArgb);
        nint screenDc = NativeMethods.GetDC(nint.Zero);
        if (screenDc == nint.Zero)
        {
            bitmap.Dispose();
            throw new InvalidOperationException("无法获取桌面设备上下文。");
        }

        try
        {
            using Graphics graphics = Graphics.FromImage(bitmap);
            nint destinationDc = graphics.GetHdc();
            try
            {
                bool succeeded = NativeMethods.BitBlt(
                    destinationDc,
                    0,
                    0,
                    bounds.Width,
                    bounds.Height,
                    screenDc,
                    bounds.Left,
                    bounds.Top,
                    NativeMethods.SrcCopy | NativeMethods.CaptureBlt);
                if (!succeeded)
                {
                    throw new InvalidOperationException("桌面画面复制失败。");
                }
            }
            finally
            {
                graphics.ReleaseHdc(destinationDc);
            }

            return bitmap;
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
        finally
        {
            _ = NativeMethods.ReleaseDC(nint.Zero, screenDc);
        }
    }

    private static bool RequiresTransientFrameFiltering(nint handle)
    {
        NativeMethods.GetWindowThreadProcessId(handle, out uint processId);
        try
        {
            using Process process = Process.GetProcessById((int)processId);
            return string.Equals(
                process.ProcessName,
                "QQMusic",
                StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static void WaitForDesktopComposition()
    {
        _ = NativeMethods.DwmFlush();
    }

    private static nint GetDesktopFilterKey(nint handle)
    {
        return (nint)(handle.ToInt64() ^ DesktopFilterKeyMask);
    }

    private static IReadOnlyList<(nint Handle, Rectangle Bounds)>
        GetStabilizedWindows()
    {
        List<(nint Handle, Rectangle Bounds)> windows = [];
        foreach (Process process in Process.GetProcessesByName("QQMusic"))
        {
            try
            {
                nint handle = process.MainWindowHandle;
                if (handle == nint.Zero ||
                    !NativeMethods.IsWindowVisible(handle) ||
                    !NativeMethods.GetWindowRect(handle, out NativeMethods.Rect rect) ||
                    rect.Width < 2 ||
                    rect.Height < 2)
                {
                    continue;
                }

                windows.Add((
                    handle,
                    new Rectangle(rect.Left, rect.Top, rect.Width, rect.Height)));
            }
            finally
            {
                process.Dispose();
            }
        }

        return windows;
    }

    public void Dispose()
    {
        _frameFilter.Dispose();
        GC.SuppressFinalize(this);
    }
}
