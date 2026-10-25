using System.Diagnostics;
using System.Drawing;
using DesktopBinarizer;
using DesktopBinarizer.Configuration;
using DesktopBinarizer.Controls;
using DesktopBinarizer.Imaging;
using DesktopBinarizer.Recording;

if (Environment.GetEnvironmentVariable("RUN_PICKER_VISUAL_TEST") == "1")
{
    RunPickerVisualTest();
    return;
}

if (Environment.GetEnvironmentVariable("RUN_QQMUSIC_CAPTURE_PROBE") == "1")
{
    RunQqMusicCaptureProbe(captureDesktop: false);
    return;
}

if (Environment.GetEnvironmentVariable("RUN_QQMUSIC_DESKTOP_PROBE") == "1")
{
    RunQqMusicCaptureProbe(captureDesktop: true);
    return;
}

Run("输出尺寸可以自定义", TestCustomOutputSize);
Run("输出尺寸支持跟随输入和输入倍数", TestOutputSizeModes);
Run("输入画面支持按比例和全屏拉伸", TestInputScaleModes);
Run("1 bit 灰度就是二值化", TestThresholdOutput);
Run("1–8 bit 灰度只产生对应灰阶", TestGrayscaleBitDepths);
Run("1–8 bit RGB 等位深只产生对应通道级别", TestUniformRgbBitDepths);
Run("RGB 分通道位深分别生效", TestPerChannelRgbBitDepths);
Run("无抖动 RGB 快速路径保持精确量化", TestDirectRgbQuantizationExact);
Run("自定义调色板只输出调色板颜色", TestCustomPalette);
Run("屏幕取色采样返回准确像素", TestScreenColorSampling);
Run("像素间距按指定颜色和宽度插入", TestPixelSpacing);
Run("像素间距在画面缩放后插入", TestPixelSpacingAfterScaling);
Run("预览间距按照逻辑输出像素边界绘制", TestPreviewPixelGridScaling);
Run("像素网格支持分数预览倍率", TestFractionalPixelGridScaling);
Run("像素间距预览严格点对点且没有外框", TestPixelPreviewRendering);
Run("预览快速换帧不会出现背景空白", TestRapidPreviewFrameSwaps);
Run("单帧合成异常会被抑制", TestTransientFrameSuppression);
Run("瞬态帧过滤器释放后拒绝调用", TestTransientFrameFilterDisposal);
Run("配置可以完整导出和导入", TestConfigurationRoundTrip);
Run("所有误差扩散算法支持彩色调色板", TestAllDitherAlgorithms);
Run("反相会交换黑白", TestInvert);
Run("无效输出尺寸和调色板会被拒绝", TestInvalidOptions);
await RunAsync("处理后画面可以录制为 MP4", TestVideoRecordingAsync);
if (Environment.GetEnvironmentVariable("RUN_AUDIO_LOOPBACK_TEST") == "1")
{
    await RunAsync("录制文件包含系统回环音频流", TestSystemAudioRecordingAsync);
}

Console.WriteLine("全部冒烟测试通过。");
return;

static void Run(string name, Action test)
{
    test();
    Console.WriteLine($"[PASS] {name}");
}

static void RunPickerVisualTest()
{
    string outputPath =
        Environment.GetEnvironmentVariable("PICKER_SCREENSHOT_PATH") ??
        Path.Combine(Path.GetTempPath(), "desktop-color-picker-ui.png");

    Application.EnableVisualStyles();
    using var picker = new ScreenColorPickerForm();
    using var timer = new System.Windows.Forms.Timer
    {
        Interval = 1500,
    };
    timer.Tick += (_, _) =>
    {
        timer.Stop();
        Rectangle bounds = SystemInformation.VirtualScreen;
        using var bitmap = new Bitmap(bounds.Width, bounds.Height);
        using (Graphics graphics = Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(
                bounds.Left,
                bounds.Top,
                0,
                0,
                bounds.Size);
        }

        bitmap.Save(outputPath);
        picker.DialogResult = DialogResult.Cancel;
        picker.Close();
    };

    timer.Start();
    picker.ShowDialog();
    Console.WriteLine(outputPath);
}

static void RunQqMusicCaptureProbe(bool captureDesktop)
{
    using var captureService =
        new DesktopBinarizer.Capture.ScreenCaptureService();
    DesktopBinarizer.Capture.WindowInfo window =
        captureService.GetCapturableWindows()
            .FirstOrDefault(window => window.ProcessName == "QQMusic")
        ?? throw new InvalidOperationException("没有找到 QQ 音乐窗口。");
    Rectangle windowBounds = ProbeNativeMethods.GetBounds(window.Handle);
    Rectangle desktopBounds = SystemInformation.VirtualScreen;

    var rows = new List<(double Mean, double BlackRatio, double WhiteRatio)>();
    string outputDirectory = Path.Combine(
        Path.GetTempPath(),
        $"qqmusic-capture-probe-{DateTime.Now:yyyyMMdd-HHmmss}");
    Directory.CreateDirectory(outputDirectory);
    double? previousMean = null;
    for (int frameIndex = 0; frameIndex < 120; frameIndex++)
    {
        using Bitmap captured = captureDesktop
            ? captureService.CaptureDesktop()
            : captureService.CaptureWindow(window.Handle);
        Rectangle sampleBounds = captureDesktop
            ? new Rectangle(
                windowBounds.Left - desktopBounds.Left,
                windowBounds.Top - desktopBounds.Top,
                windowBounds.Width,
                windowBounds.Height)
            : new Rectangle(Point.Empty, captured.Size);
        sampleBounds.Intersect(new Rectangle(Point.Empty, captured.Size));
        using Bitmap bitmap = captured.Clone(
            sampleBounds,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        long luminanceTotal = 0;
        int blackCount = 0;
        int whiteCount = 0;
        int sampleCount = 0;
        for (int y = 0; y < bitmap.Height; y += 16)
        {
            for (int x = 0; x < bitmap.Width; x += 16)
            {
                Color color = bitmap.GetPixel(x, y);
                int luminance =
                    ((color.R * 54) + (color.G * 183) + (color.B * 19)) >> 8;
                luminanceTotal += luminance;
                blackCount += luminance < 8 ? 1 : 0;
                whiteCount += luminance > 247 ? 1 : 0;
                sampleCount++;
            }
        }

        double mean = luminanceTotal / (double)sampleCount;
        rows.Add((
            mean,
            blackCount / (double)sampleCount,
            whiteCount / (double)sampleCount));
        if (frameIndex == 0 ||
            previousMean is null ||
            Math.Abs(mean - previousMean.Value) >= 5)
        {
            bitmap.Save(
                Path.Combine(
                    outputDirectory,
                    $"frame-{frameIndex:D3}-mean-{mean:F2}.png"));
        }

        previousMean = mean;
        Thread.Sleep(15);
    }

    Console.WriteLine(
        $"QQ 音乐（{(captureDesktop ? "全桌面区域" : "指定窗口")}）：{window.Title}");
    Console.WriteLine($"跳变帧目录：{outputDirectory}");
    Console.WriteLine(
        $"平均亮度：{rows.Min(row => row.Mean):F3}–" +
        $"{rows.Max(row => row.Mean):F3}");
    Console.WriteLine(
        $"黑色比例：{rows.Min(row => row.BlackRatio):F4}–" +
        $"{rows.Max(row => row.BlackRatio):F4}");
    Console.WriteLine(
        $"白色比例：{rows.Min(row => row.WhiteRatio):F4}–" +
        $"{rows.Max(row => row.WhiteRatio):F4}");
    foreach ((double mean, double blackRatio, double whiteRatio) in rows
        .OrderBy(row => row.Mean)
        .Take(5))
    {
        Console.WriteLine(
            $"低值帧 mean={mean:F3} black={blackRatio:F4} white={whiteRatio:F4}");
    }
}

static async Task RunAsync(string name, Func<Task> test)
{
    await test();
    Console.WriteLine($"[PASS] {name}");
}

static void TestCustomOutputSize()
{
    using var source = new Bitmap(23, 17);
    using Bitmap output = ImageProcessor.Process(
        source,
        new ImageProcessingOptions(OutputWidth: 123, OutputHeight: 77));
    Assert(output.Size == new Size(123, 77), $"实际尺寸为 {output.Size}");
}

static void TestOutputSizeModes()
{
    Size input = new(640, 360);
    Size followed = OutputSizeCalculator.Resolve(
        input,
        OutputSizeMode.FollowInput,
        new Size(400, 300),
        1);
    Size multiplied = OutputSizeCalculator.Resolve(
        input,
        OutputSizeMode.InputMultiplier,
        new Size(400, 300),
        0.5);

    Assert(followed == input, $"跟随输入得到 {followed}");
    Assert(multiplied == new Size(320, 180), $"输入倍数得到 {multiplied}");
}

static void TestInputScaleModes()
{
    using var source = new Bitmap(2, 1);
    using (Graphics graphics = Graphics.FromImage(source))
    {
        graphics.Clear(Color.White);
    }

    using Bitmap proportional = ImageProcessor.Process(
        source,
        new ImageProcessingOptions(
            OutputWidth: 4,
            OutputHeight: 4,
            DitherAlgorithm: DitherAlgorithm.None,
            InputScaleMode: InputScaleMode.Proportional));
    using Bitmap stretched = ImageProcessor.Process(
        source,
        new ImageProcessingOptions(
            OutputWidth: 4,
            OutputHeight: 4,
            DitherAlgorithm: DitherAlgorithm.None,
            InputScaleMode: InputScaleMode.Stretch));

    Assert(proportional.GetPixel(0, 0).R == 0, "按比例缩放没有产生黑边");
    Assert(proportional.GetPixel(0, 2).R == 255, "按比例缩放丢失了输入画面");
    Assert(stretched.GetPixel(0, 0).R == 255, "全屏拉伸没有铺满输出");
}

static void TestThresholdOutput()
{
    using Bitmap source = CreateGradient();
    using Bitmap output = ImageProcessor.Process(
        source,
        new ImageProcessingOptions(
            OutputWidth: 96,
            OutputHeight: 64,
            ColorMode: ColorQuantizationMode.Grayscale,
            GrayscaleBits: 1,
            Threshold: 128,
            DitherAlgorithm: DitherAlgorithm.None));
    AssertBinary(output);
}

static void TestGrayscaleBitDepths()
{
    using Bitmap source = CreateGradient();

    for (int bits = 1; bits <= 8; bits++)
    {
        using Bitmap output = ImageProcessor.Process(
            source,
            new ImageProcessingOptions(
                OutputWidth: 64,
                OutputHeight: 64,
                ColorMode: ColorQuantizationMode.Grayscale,
                GrayscaleBits: bits,
                DitherAlgorithm: DitherAlgorithm.None));
        HashSet<byte> allowed = GetAllowedChannelValues(bits);

        ForEachPixel(output, color =>
        {
            Assert(color.R == color.G && color.G == color.B, "灰度输出包含彩色像素");
            Assert(allowed.Contains(color.R), $"{bits} bit 灰度出现非法值 {color.R}");
        });
    }
}

static void TestUniformRgbBitDepths()
{
    using Bitmap source = CreateColorGradient();

    for (int bits = 1; bits <= 8; bits++)
    {
        using Bitmap output = ImageProcessor.Process(
            source,
            new ImageProcessingOptions(
                OutputWidth: 64,
                OutputHeight: 64,
                ColorMode: ColorQuantizationMode.RgbUniform,
                RgbBits: bits,
                DitherAlgorithm: DitherAlgorithm.None));
        HashSet<byte> allowed = GetAllowedChannelValues(bits);

        ForEachPixel(output, color =>
        {
            Assert(allowed.Contains(color.R), $"{bits} bit RGB 出现非法 R 值 {color.R}");
            Assert(allowed.Contains(color.G), $"{bits} bit RGB 出现非法 G 值 {color.G}");
            Assert(allowed.Contains(color.B), $"{bits} bit RGB 出现非法 B 值 {color.B}");
        });
    }
}

static void TestPerChannelRgbBitDepths()
{
    using Bitmap source = CreateColorGradient();
    using Bitmap output = ImageProcessor.Process(
        source,
        new ImageProcessingOptions(
            OutputWidth: 64,
            OutputHeight: 64,
            ColorMode: ColorQuantizationMode.RgbPerChannel,
            RedBits: 2,
            GreenBits: 3,
            BlueBits: 5,
            DitherAlgorithm: DitherAlgorithm.None));

    HashSet<byte> redValues = GetAllowedChannelValues(2);
    HashSet<byte> greenValues = GetAllowedChannelValues(3);
    HashSet<byte> blueValues = GetAllowedChannelValues(5);
    ForEachPixel(output, color =>
    {
        Assert(redValues.Contains(color.R), $"R 通道出现非法值 {color.R}");
        Assert(greenValues.Contains(color.G), $"G 通道出现非法值 {color.G}");
        Assert(blueValues.Contains(color.B), $"B 通道出现非法值 {color.B}");
    });
}

static void TestDirectRgbQuantizationExact()
{
    using var source = new Bitmap(256, 1);
    for (int value = 0; value < 256; value++)
    {
        source.SetPixel(
            value,
            0,
            Color.FromArgb(value, 255 - value, (value * 73) & 0xFF));
    }

    for (int bits = 1; bits <= 8; bits++)
    {
        using Bitmap output = ImageProcessor.Process(
            source,
            new ImageProcessingOptions(
                OutputWidth: source.Width,
                OutputHeight: source.Height,
                ColorMode: ColorQuantizationMode.RgbUniform,
                RgbBits: bits,
                DitherAlgorithm: DitherAlgorithm.None,
                InputScaleMode: InputScaleMode.Stretch));

        for (int value = 0; value < 256; value++)
        {
            Color actual = output.GetPixel(value, 0);
            Assert(
                actual.R == QuantizeChannelExpected(value, bits),
                $"{bits} bit R 通道快速路径在 {value} 处不精确");
            Assert(
                actual.G == QuantizeChannelExpected(255 - value, bits),
                $"{bits} bit G 通道快速路径在 {value} 处不精确");
            Assert(
                actual.B == QuantizeChannelExpected((value * 73) & 0xFF, bits),
                $"{bits} bit B 通道快速路径在 {value} 处不精确");
        }
    }

    using Bitmap inverted = ImageProcessor.Process(
        source,
        new ImageProcessingOptions(
            OutputWidth: source.Width,
            OutputHeight: source.Height,
            ColorMode: ColorQuantizationMode.RgbPerChannel,
            RedBits: 2,
            GreenBits: 3,
            BlueBits: 5,
            DitherAlgorithm: DitherAlgorithm.None,
            Invert: true,
            InputScaleMode: InputScaleMode.Stretch));
    for (int value = 0; value < 256; value++)
    {
        Color actual = inverted.GetPixel(value, 0);
        Assert(
            actual.R == QuantizeChannelExpected(255 - value, 2),
            $"反相 R 通道快速路径在 {value} 处不精确");
        Assert(
            actual.G == QuantizeChannelExpected(value, 3),
            $"反相 G 通道快速路径在 {value} 处不精确");
        Assert(
            actual.B == QuantizeChannelExpected(255 - ((value * 73) & 0xFF), 5),
            $"反相 B 通道快速路径在 {value} 处不精确");
    }
}

static void TestCustomPalette()
{
    Color[] palette = [Color.Black, Color.Red, Color.Lime, Color.Blue, Color.White];
    using Bitmap source = CreateColorGradient();
    using Bitmap output = ImageProcessor.Process(
        source,
        new ImageProcessingOptions(
            OutputWidth: 64,
            OutputHeight: 64,
            ColorMode: ColorQuantizationMode.CustomPalette,
            Palette: palette,
            DitherAlgorithm: DitherAlgorithm.None));
    AssertUsesOnlyPalette(output, palette);
}

static void TestScreenColorSampling()
{
    using var bitmap = new Bitmap(3, 2);
    Color expected = Color.FromArgb(37, 149, 221);
    bitmap.SetPixel(2, 1, expected);

    Color sampled = ScreenColorPickerForm.SampleColor(bitmap, new Point(2, 1));
    Assert(sampled.ToArgb() == expected.ToArgb(), $"实际采样颜色为 {sampled}");
}

static void TestPixelSpacing()
{
    using var source = new Bitmap(2, 2);
    source.SetPixel(0, 0, Color.Red);
    source.SetPixel(1, 0, Color.Lime);
    source.SetPixel(0, 1, Color.Blue);
    source.SetPixel(1, 1, Color.White);
    Color spacingColor = Color.Magenta;
    Color[] palette =
        [Color.Red, Color.Lime, Color.Blue, Color.White, spacingColor];

    using Bitmap output = ImageProcessor.Process(
        source,
        new ImageProcessingOptions(
            OutputWidth: 2,
            OutputHeight: 2,
            ColorMode: ColorQuantizationMode.CustomPalette,
            Palette: palette,
            DitherAlgorithm: DitherAlgorithm.None,
            InputScaleMode: InputScaleMode.Stretch,
            EnablePixelSpacing: true,
            PixelSpacingWidth: 2,
            PixelSpacingColor: spacingColor));

    Assert(output.Size == new Size(4, 4), $"间距输出尺寸为 {output.Size}");
    Assert(output.GetPixel(0, 0).ToArgb() == Color.Red.ToArgb(), "左上像素错误");
    Assert(output.GetPixel(3, 0).ToArgb() == Color.Lime.ToArgb(), "右上像素错误");
    Assert(output.GetPixel(0, 3).ToArgb() == Color.Blue.ToArgb(), "左下像素错误");
    Assert(output.GetPixel(3, 3).ToArgb() == Color.White.ToArgb(), "右下像素错误");

    for (int index = 0; index < output.Width; index++)
    {
        Assert(
            output.GetPixel(index, 1).ToArgb() == spacingColor.ToArgb(),
            "水平间距颜色错误");
        Assert(
            output.GetPixel(index, 2).ToArgb() == spacingColor.ToArgb(),
            "水平间距颜色错误");
        Assert(
            output.GetPixel(1, index).ToArgb() == spacingColor.ToArgb(),
            "垂直间距颜色错误");
        Assert(
            output.GetPixel(2, index).ToArgb() == spacingColor.ToArgb(),
            "垂直间距颜色错误");
    }
}

static void TestPixelSpacingAfterScaling()
{
    using var source = new Bitmap(2, 1);
    source.SetPixel(0, 0, Color.Black);
    source.SetPixel(1, 0, Color.White);

    using Bitmap scaled = ImageProcessor.Process(
        source,
        new ImageProcessingOptions(
            OutputWidth: 4,
            OutputHeight: 2,
            GrayscaleBits: 1,
            DitherAlgorithm: DitherAlgorithm.None,
            InputScaleMode: InputScaleMode.Stretch));
    using Bitmap spaced = ImageProcessor.ApplyPixelSpacing(
        scaled,
        1,
        Color.Red);

    Assert(scaled.Size == new Size(4, 2), $"缩放尺寸为 {scaled.Size}");
    Assert(spaced.Size == new Size(7, 3), $"加间距后的尺寸为 {spaced.Size}");
    Assert(spaced.GetPixel(1, 0).ToArgb() == Color.Red.ToArgb(), "横向间距未固定为 1 px");
    Assert(spaced.GetPixel(0, 1).ToArgb() == Color.Red.ToArgb(), "纵向间距未固定为 1 px");
}

static void TestPreviewPixelGridScaling()
{
    Size previewSize = PixelGridRenderer.CalculateOutputSize(
        new Size(192, 64),
        2,
        1);
    Assert(
        previewSize == new Size(575, 191),
        $"192 × 64 在 2× 下的网格尺寸为 {previewSize}");

    using var source = new Bitmap(2, 2);
    source.SetPixel(0, 0, Color.Red);
    source.SetPixel(1, 0, Color.Lime);
    source.SetPixel(0, 1, Color.Blue);
    source.SetPixel(1, 1, Color.White);
    using Bitmap preview = PixelGridRenderer.Render(
        source,
        2,
        1,
        Color.Magenta);

    Assert(preview.Size == new Size(5, 5), $"2 × 2 网格预览尺寸为 {preview.Size}");
    Assert(preview.GetPixel(0, 0).ToArgb() == Color.Red.ToArgb(), "左上色块错误");
    Assert(preview.GetPixel(1, 1).ToArgb() == Color.Red.ToArgb(), "左上色块不是 2 × 2");
    Assert(preview.GetPixel(3, 0).ToArgb() == Color.Lime.ToArgb(), "右上色块错误");
    Assert(preview.GetPixel(4, 1).ToArgb() == Color.Lime.ToArgb(), "右上色块不是 2 × 2");
    Assert(preview.GetPixel(0, 3).ToArgb() == Color.Blue.ToArgb(), "左下色块错误");
    Assert(preview.GetPixel(3, 3).ToArgb() == Color.White.ToArgb(), "右下色块错误");

    for (int index = 0; index < preview.Width; index++)
    {
        Assert(
            preview.GetPixel(index, 2).ToArgb() == Color.Magenta.ToArgb(),
            "横向间距不在逻辑像素边界");
        Assert(
            preview.GetPixel(2, index).ToArgb() == Color.Magenta.ToArgb(),
            "纵向间距不在逻辑像素边界");
    }
}

static void TestFractionalPixelGridScaling()
{
    using var source = new Bitmap(2, 1);
    source.SetPixel(0, 0, Color.Red);
    source.SetPixel(1, 0, Color.Lime);

    using Bitmap preview = PixelGridRenderer.Render(
        source,
        1.5,
        1,
        Color.Magenta);

    Assert(preview.Size == new Size(4, 2), $"1.5× 网格尺寸为 {preview.Size}");
    for (int y = 0; y < preview.Height; y++)
    {
        Assert(preview.GetPixel(0, y).ToArgb() == Color.Red.ToArgb(), "分数倍率红色像素错误");
        Assert(preview.GetPixel(1, y).ToArgb() == Color.Red.ToArgb(), "分数倍率红色宽度错误");
        Assert(preview.GetPixel(2, y).ToArgb() == Color.Magenta.ToArgb(), "分数倍率间距错误");
        Assert(preview.GetPixel(3, y).ToArgb() == Color.Lime.ToArgb(), "分数倍率绿色像素错误");
    }
}

static void TestPixelPreviewRendering()
{
    Exception? failure = null;
    var thread = new Thread(() =>
    {
        try
        {
            TestPixelPreviewRenderingOnStaThread();
        }
        catch (Exception exception)
        {
            failure = exception;
        }
    });
    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();

    if (!thread.Join(TimeSpan.FromSeconds(10)))
    {
        throw new TimeoutException("像素预览控件绘制测试超时。");
    }

    if (failure is not null)
    {
        System.Runtime.ExceptionServices.ExceptionDispatchInfo
            .Capture(failure)
            .Throw();
    }
}

static void TestPixelPreviewRenderingOnStaThread()
{
    using var source = new Bitmap(2, 2);
    source.SetPixel(0, 0, Color.Red);
    source.SetPixel(1, 0, Color.Lime);
    source.SetPixel(0, 1, Color.Blue);
    source.SetPixel(1, 1, Color.White);

    using var preview = new PixelPreview
    {
        Size = new Size(29, 29),
        DisplayScale = 2,
    };
    preview.SetPreviewFrame(
        new Bitmap(source),
        showPixelSpacing: true,
        pixelSpacingWidth: 1,
        pixelSpacingColor: Color.Magenta);
    using var host = new Form
    {
        ClientSize = preview.Size,
        FormBorderStyle = FormBorderStyle.None,
        ShowInTaskbar = false,
        StartPosition = FormStartPosition.Manual,
        Location = new Point(-32000, -32000),
    };
    host.Controls.Add(preview);
    host.CreateControl();
    preview.CreateControl();

    using var rendered = new Bitmap(preview.Width, preview.Height);
    preview.DrawToBitmap(rendered, preview.ClientRectangle);

    const int left = 12;
    const int top = 12;
    Assert(rendered.GetPixel(left, top).ToArgb() == Color.Red.ToArgb(), "外框覆盖左上像素");
    Assert(rendered.GetPixel(left + 1, top + 1).ToArgb() == Color.Red.ToArgb(), "红色色块被模糊");
    Assert(rendered.GetPixel(left + 3, top).ToArgb() == Color.Lime.ToArgb(), "绿色色块被模糊");
    Assert(rendered.GetPixel(left, top + 3).ToArgb() == Color.Blue.ToArgb(), "蓝色色块被模糊");
    Assert(rendered.GetPixel(left + 4, top + 4).ToArgb() == Color.White.ToArgb(), "外框覆盖右下像素");

    for (int index = 0; index < 5; index++)
    {
        Assert(
            rendered.GetPixel(left + index, top + 2).ToArgb() ==
            Color.Magenta.ToArgb(),
            "横向间距产生了中间色");
        Assert(
            rendered.GetPixel(left + 2, top + index).ToArgb() ==
            Color.Magenta.ToArgb(),
            "纵向间距产生了中间色");
    }
}

static void TestRapidPreviewFrameSwaps()
{
    Exception? failure = null;
    var thread = new Thread(() =>
    {
        try
        {
            using var preview = new PixelPreview
            {
                Size = new Size(120, 100),
                DisplayScale = 2,
            };
            using var host = new Form
            {
                ClientSize = preview.Size,
                FormBorderStyle = FormBorderStyle.None,
                ShowInTaskbar = false,
                StartPosition = FormStartPosition.Manual,
                Location = new Point(-32000, -32000),
            };
            host.Controls.Add(preview);
            host.CreateControl();
            preview.CreateControl();

            for (int frameIndex = 0; frameIndex < 120; frameIndex++)
            {
                Color frameColor = frameIndex % 2 == 0 ? Color.Red : Color.Lime;
                var frame = new Bitmap(16, 12);
                using (Graphics graphics = Graphics.FromImage(frame))
                {
                    graphics.Clear(frameColor);
                }

                preview.SetPreviewFrame(
                    frame,
                    showPixelSpacing: true,
                    pixelSpacingWidth: 1,
                    pixelSpacingColor: Color.Blue);

                using var rendered = new Bitmap(preview.Width, preview.Height);
                preview.DrawToBitmap(rendered, preview.ClientRectangle);
                Color sample = rendered.GetPixel(36, 32);
                Assert(
                    sample.ToArgb() == frameColor.ToArgb(),
                    $"第 {frameIndex} 帧出现空白或残留颜色 {sample}");
            }
        }
        catch (Exception exception)
        {
            failure = exception;
        }
    });
    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();

    if (!thread.Join(TimeSpan.FromSeconds(15)))
    {
        throw new TimeoutException("预览快速换帧测试超时。");
    }

    if (failure is not null)
    {
        System.Runtime.ExceptionServices.ExceptionDispatchInfo
            .Capture(failure)
            .Throw();
    }
}

static void TestTransientFrameSuppression()
{
    using var filter = new DesktopBinarizer.Capture.TransientFrameFilter();
    nint key = 42;
    using Bitmap stable = CreateSolidBitmap(80, 60, Color.FromArgb(24, 24, 24));
    using Bitmap transient = CreateSolidBitmap(80, 60, Color.Cyan);
    using Bitmap nextStable = CreateSolidBitmap(80, 60, Color.FromArgb(26, 26, 26));
    using Bitmap newPageFirst = CreateSolidBitmap(80, 60, Color.White);
    using Bitmap newPageSecond = CreateSolidBitmap(80, 60, Color.FromArgb(248, 248, 248));

    using Bitmap first = filter.Stabilize(key, stable);
    using Bitmap suppressed = filter.Stabilize(key, transient);
    using Bitmap recovered = filter.Stabilize(key, nextStable);
    using Bitmap delayedPage = filter.Stabilize(key, newPageFirst);
    using Bitmap acceptedPage = filter.Stabilize(key, newPageSecond);

    Assert(first.GetPixel(0, 0).ToArgb() == stable.GetPixel(0, 0).ToArgb(), "初始帧错误");
    Assert(
        suppressed.GetPixel(0, 0).ToArgb() == stable.GetPixel(0, 0).ToArgb(),
        "单帧合成异常没有被抑制");
    Assert(
        recovered.GetPixel(0, 0).ToArgb() == nextStable.GetPixel(0, 0).ToArgb(),
        "恢复后的正常帧没有通过");
    Assert(
        delayedPage.GetPixel(0, 0).ToArgb() == nextStable.GetPixel(0, 0).ToArgb(),
        "大幅页面切换的首帧没有延迟确认");
    Assert(
        acceptedPage.GetPixel(0, 0).ToArgb() == newPageSecond.GetPixel(0, 0).ToArgb(),
        "持续的新页面没有在第二帧通过");
}

static void TestTransientFrameFilterDisposal()
{
    var filter = new DesktopBinarizer.Capture.TransientFrameFilter();
    filter.Dispose();
    filter.Dispose();

    using Bitmap frame = CreateSolidBitmap(8, 8, Color.Black);
    AssertThrows<ObjectDisposedException>(() =>
    {
        using Bitmap _ = filter.Stabilize(1, frame);
    });
}

static void TestConfigurationRoundTrip()
{
    var expected = new BinarizerConfiguration
    {
        CaptureSource = CaptureSourceMode.Window,
        WindowTitle = "测试窗口",
        InputScaleMode = InputScaleMode.Stretch,
        OutputSizeMode = OutputSizeMode.InputMultiplier,
        OutputWidth = 1920,
        OutputHeight = 1080,
        OutputMultiplier = 0.5,
        PreviewScale = 3,
        ShowOriginalPreview = true,
        ColorMode = ColorQuantizationMode.RgbPerChannel,
        GrayscaleBits = 3,
        Threshold = 96,
        RgbBits = 5,
        RedBits = 2,
        GreenBits = 4,
        BlueBits = 6,
        Palette = ["#000000", "#E04080", "#FFFFFF"],
        DitherAlgorithm = DitherAlgorithm.Stucki,
        Invert = true,
        EnablePixelSpacing = true,
        PixelSpacingWidth = 3,
        PixelSpacingColor = "#E04080",
        FrameRate = 120,
        CaptureSystemAudio = false,
    };

    string path = Path.Combine(
        Path.GetTempPath(),
        $"desktop-binarizer-config-{Guid.NewGuid():N}.json");
    BinarizerConfiguration actual;
    try
    {
        ConfigurationFile.Save(path, expected);
        actual = ConfigurationFile.Load(path);
    }
    finally
    {
        File.Delete(path);
    }

    Assert(actual.CaptureSource == expected.CaptureSource, "抓取来源未恢复");
    Assert(actual.WindowTitle == expected.WindowTitle, "窗口标题未恢复");
    Assert(actual.InputScaleMode == expected.InputScaleMode, "输入缩放未恢复");
    Assert(actual.OutputSizeMode == expected.OutputSizeMode, "输出模式未恢复");
    Assert(actual.OutputWidth == 1920 && actual.OutputHeight == 1080, "分辨率未恢复");
    Assert(actual.OutputMultiplier == 0.5, "输出倍数未恢复");
    Assert(actual.PreviewScale == 3, "预览倍率未恢复");
    Assert(actual.ShowOriginalPreview, "原画面预览设置未恢复");
    Assert(actual.ColorMode == expected.ColorMode, "颜色模式未恢复");
    Assert(actual.RedBits == 2 && actual.GreenBits == 4 && actual.BlueBits == 6, "位深未恢复");
    Assert(actual.Palette.SequenceEqual(expected.Palette), "调色板未恢复");
    Assert(actual.DitherAlgorithm == DitherAlgorithm.Stucki, "抖动算法未恢复");
    Assert(actual.EnablePixelSpacing && actual.PixelSpacingWidth == 3, "像素间距未恢复");
    Assert(actual.PixelSpacingColor == "#E04080", "像素间距颜色未恢复");
    Assert(actual.FrameRate == 120 && !actual.CaptureSystemAudio, "录制设置未恢复");

    bool rejected = false;
    try
    {
        ConfigurationFile.Serialize(expected with { OutputWidth = 15 });
    }
    catch (InvalidDataException)
    {
        rejected = true;
    }

    Assert(rejected, "小于 16 的最终自定义分辨率没有被拒绝");
}

static void TestAllDitherAlgorithms()
{
    using Bitmap source = CreateColorGradient();
    Color[] palette =
    [
        Color.Black,
        Color.FromArgb(255, 96, 0),
        Color.FromArgb(0, 180, 255),
        Color.White,
    ];

    foreach (DitherAlgorithm algorithm in Enum.GetValues<DitherAlgorithm>())
    {
        using Bitmap output = ImageProcessor.Process(
            source,
            new ImageProcessingOptions(
                OutputWidth: 96,
                OutputHeight: 64,
                ColorMode: ColorQuantizationMode.CustomPalette,
                Palette: palette,
                DitherAlgorithm: algorithm));
        AssertUsesOnlyPalette(output, palette);
    }
}

static void TestInvert()
{
    using var source = new Bitmap(1, 1);
    source.SetPixel(0, 0, Color.White);

    using Bitmap normal = ImageProcessor.Process(
        source,
        new ImageProcessingOptions(
            OutputWidth: 1,
            OutputHeight: 1,
            DitherAlgorithm: DitherAlgorithm.None,
            Invert: false));
    using Bitmap inverted = ImageProcessor.Process(
        source,
        new ImageProcessingOptions(
            OutputWidth: 1,
            OutputHeight: 1,
            DitherAlgorithm: DitherAlgorithm.None,
            Invert: true));

    Assert(normal.GetPixel(0, 0).R == 255, "白色输入未输出白色");
    Assert(inverted.GetPixel(0, 0).R == 0, "反相后未输出黑色");
}

static void TestInvalidOptions()
{
    using var source = new Bitmap(1, 1);
    AssertThrows<ArgumentOutOfRangeException>(() =>
    {
        using Bitmap _ = ImageProcessor.Process(
            source,
            new ImageProcessingOptions(OutputWidth: 0));
    });
    AssertThrows<ArgumentException>(() =>
    {
        using Bitmap _ = ImageProcessor.Process(
            source,
            new ImageProcessingOptions(
                ColorMode: ColorQuantizationMode.CustomPalette,
                Palette: [Color.Black]));
    });
}

static async Task TestVideoRecordingAsync()
{
    string outputPath = Path.Combine(
        Path.GetTempPath(),
        $"desktop-binarizer-test-{Guid.NewGuid():N}.mp4");

    try
    {
        await using var recorder = new VideoRecorder();
        await recorder.StartAsync(new RecordingOptions(
            outputPath,
            Width: 64,
            Height: 48,
            FramesPerSecond: 20,
            CaptureSystemAudio: false));

        using var frame = new Bitmap(64, 48);
        using (Graphics graphics = Graphics.FromImage(frame))
        {
            graphics.Clear(Color.Cyan);
            graphics.FillRectangle(Brushes.Magenta, 8, 8, 24, 24);
        }

        recorder.SubmitFrame(frame);
        await Task.Delay(750);
        await recorder.StopAsync();

        var file = new FileInfo(outputPath);
        Assert(file.Exists && file.Length > 1000, "没有生成有效的 MP4 文件");
    }
    finally
    {
        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }
    }
}

static async Task TestSystemAudioRecordingAsync()
{
    string outputPath = Path.Combine(
        Path.GetTempPath(),
        $"desktop-binarizer-audio-test-{Guid.NewGuid():N}.mp4");

    try
    {
        await using var recorder = new VideoRecorder();
        await recorder.StartAsync(new RecordingOptions(
            outputPath,
            Width: 64,
            Height: 48,
            FramesPerSecond: 20,
            CaptureSystemAudio: true));

        using var frame = new Bitmap(64, 48);
        using (Graphics graphics = Graphics.FromImage(frame))
        {
            graphics.Clear(Color.White);
        }

        recorder.SubmitFrame(frame);
        await Task.Delay(1500);
        await recorder.StopAsync();

        string ffmpegPath = Path.Combine(
            AppContext.BaseDirectory,
            "ffmpeg",
            "ffmpeg.exe");
        var startInfo = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add("-hide_banner");
        startInfo.ArgumentList.Add("-i");
        startInfo.ArgumentList.Add(outputPath);
        startInfo.ArgumentList.Add("-f");
        startInfo.ArgumentList.Add("null");
        startInfo.ArgumentList.Add("-");

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("无法启动 FFmpeg 检查录制文件。");
        string diagnostics = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        Assert(process.ExitCode == 0, "FFmpeg 无法读取带音频的录制文件");
        Assert(diagnostics.Contains("Audio:", StringComparison.Ordinal), "录制文件中没有音频流");
    }
    finally
    {
        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }
    }
}

static Bitmap CreateGradient()
{
    var bitmap = new Bitmap(64, 64);
    for (int y = 0; y < bitmap.Height; y++)
    {
        for (int x = 0; x < bitmap.Width; x++)
        {
            int value = (x * 3 + y * 2) % 256;
            bitmap.SetPixel(x, y, Color.FromArgb(value, value, value));
        }
    }

    return bitmap;
}

static Bitmap CreateSolidBitmap(int width, int height, Color color)
{
    var bitmap = new Bitmap(width, height);
    using Graphics graphics = Graphics.FromImage(bitmap);
    graphics.Clear(color);
    return bitmap;
}

static Bitmap CreateColorGradient()
{
    var bitmap = new Bitmap(64, 64);
    for (int y = 0; y < bitmap.Height; y++)
    {
        for (int x = 0; x < bitmap.Width; x++)
        {
            bitmap.SetPixel(
                x,
                y,
                Color.FromArgb(
                    x * 255 / 63,
                    y * 255 / 63,
                    (x + y) * 255 / 126));
        }
    }

    return bitmap;
}

static HashSet<byte> GetAllowedChannelValues(int bits)
{
    int maximumLevel = (1 << bits) - 1;
    return Enumerable
        .Range(0, maximumLevel + 1)
        .Select(level => (byte)Math.Round(level * 255.0 / maximumLevel))
        .ToHashSet();
}

static byte QuantizeChannelExpected(int value, int bits)
{
    int maximumLevel = (1 << bits) - 1;
    int quantizedLevel = (int)Math.Round(value * maximumLevel / 255.0);
    return (byte)Math.Round(quantizedLevel * 255.0 / maximumLevel);
}

static void AssertBinary(Bitmap bitmap)
{
    ForEachPixel(bitmap, color =>
    {
        bool isBinary = color.R is 0 or 255 &&
                        color.G == color.R &&
                        color.B == color.R;
        Assert(isBinary, $"像素不是黑白值：{color}");
    });
}

static void AssertUsesOnlyPalette(Bitmap bitmap, IReadOnlyList<Color> palette)
{
    HashSet<int> allowed = palette.Select(color => color.ToArgb()).ToHashSet();
    ForEachPixel(bitmap, color =>
        Assert(allowed.Contains(color.ToArgb()), $"输出了调色板外颜色：{color}"));
}

static void ForEachPixel(Bitmap bitmap, Action<Color> assertion)
{
    for (int y = 0; y < bitmap.Height; y++)
    {
        for (int x = 0; x < bitmap.Width; x++)
        {
            assertion(bitmap.GetPixel(x, y));
        }
    }
}

static void AssertThrows<TException>(Action action)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException($"没有抛出 {typeof(TException).Name}");
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

internal static class ProbeNativeMethods
{
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool GetWindowRect(
        nint handle,
        out NativeRect rectangle);

    public static Rectangle GetBounds(nint handle)
    {
        if (!GetWindowRect(handle, out NativeRect rectangle))
        {
            throw new InvalidOperationException("无法读取 QQ 音乐窗口位置。");
        }

        return Rectangle.FromLTRB(
            rectangle.Left,
            rectangle.Top,
            rectangle.Right,
            rectangle.Bottom);
    }

    [System.Runtime.InteropServices.StructLayout(
        System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
