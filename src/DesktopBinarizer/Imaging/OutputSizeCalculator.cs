namespace DesktopBinarizer.Imaging;

public static class OutputSizeCalculator
{
    public const int MaximumDimension = 8192;

    public static Size Resolve(
        Size inputSize,
        OutputSizeMode mode,
        Size customSize,
        double inputMultiplier)
    {
        if (inputSize.Width <= 0 || inputSize.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(inputSize));
        }

        Size result = mode switch
        {
            OutputSizeMode.Custom => customSize,
            OutputSizeMode.FollowInput => inputSize,
            OutputSizeMode.InputMultiplier => new Size(
                Math.Max(1, (int)Math.Round(inputSize.Width * inputMultiplier)),
                Math.Max(1, (int)Math.Round(inputSize.Height * inputMultiplier))),
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        };

        if (inputMultiplier <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(inputMultiplier));
        }

        if (result.Width is < 1 or > MaximumDimension ||
            result.Height is < 1 or > MaximumDimension)
        {
            throw new InvalidOperationException(
                AppLocalizer.Format(
                    "CalculatedOutputOutOfRange",
                    result.Width,
                    result.Height,
                    MaximumDimension));
        }

        return result;
    }
}
