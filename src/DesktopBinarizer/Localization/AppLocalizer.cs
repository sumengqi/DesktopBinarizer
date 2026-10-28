using System.Globalization;

namespace DesktopBinarizer;

public static class AppLocalizer
{
    private sealed record Entry(string Chinese, string English);

    private static readonly IReadOnlyDictionary<string, Entry> Entries =
        new Dictionary<string, Entry>(StringComparer.Ordinal)
        {
            ["AppTitle"] = new("桌面色彩量化实时预览", "Desktop Color Quantizer"),
            ["Language"] = new("语言", "Language"),
            ["ChineseSimplified"] = new("简体中文", "Simplified Chinese"),
            ["English"] = new("English", "English"),
            ["HeaderTitle"] = new("实时色彩量化", "Real-time Color Quantization"),
            ["Subtitle"] = new(
                "灰度、低位深 RGB、自定义调色板与视频录制",
                "Grayscale, low-bit RGB, custom palettes, and video recording"),
            ["ImportConfiguration"] = new("导入配置", "Import Settings"),
            ["ExportConfiguration"] = new("导出配置", "Export Settings"),
            ["Desktop"] = new("整个桌面", "Entire Desktop"),
            ["Window"] = new("指定窗口", "Specific Window"),
            ["ScaleProportional"] = new("按比例缩放（完整画面）", "Fit Proportionally (Full Image)"),
            ["ScaleStretch"] = new("全屏拉伸（铺满输出）", "Stretch to Fill Output"),
            ["Refresh"] = new("刷新", "Refresh"),
            ["OutputCustom"] = new("自定义", "Custom"),
            ["OutputFollowInput"] = new("跟随输入", "Match Input"),
            ["OutputInputMultiplier"] = new("输入尺寸倍数", "Input Size Multiplier"),
            ["PreviewFit"] = new("适应窗口", "Fit to Window"),
            ["Preview0125"] = new("0.125×", "0.125×"),
            ["Preview025"] = new("0.25×", "0.25×"),
            ["Preview0333"] = new("0.333×", "0.333×"),
            ["Preview05"] = new("0.5×", "0.5×"),
            ["Preview1"] = new("1×", "1×"),
            ["Preview2"] = new("2×", "2×"),
            ["Preview3"] = new("3×", "3×"),
            ["Preview4"] = new("4×", "4×"),
            ["Preview8"] = new("8×", "8×"),
            ["ShowOriginalPreview"] = new("同时显示原画面预览", "Show Original Preview"),
            ["ColorModeGrayscale"] = new("灰度（1 bit 为二值化）", "Grayscale (1 bit is Binary)"),
            ["ColorModeRgbUniform"] = new("RGB 等位深（每通道 1–8 bit）", "RGB Equal Depth (1–8 bits/channel)"),
            ["ColorModeRgbPerChannel"] = new("RGB 分通道位深", "RGB Per-channel Depth"),
            ["ColorModeCustomPalette"] = new("自定义调色板", "Custom Palette"),
            ["GrayscaleDepth"] = new("灰度位深", "Grayscale Depth"),
            ["BitRange"] = new("1–8 bit", "1–8 bits"),
            ["PerChannelDepth"] = new("每通道位深", "Per-channel Depth"),
            ["SameRgb"] = new("R/G/B 同值", "Same for R/G/B"),
            ["RedDepth"] = new("红色 R（bit）", "Red R (bits)"),
            ["GreenDepth"] = new("绿色 G（bit）", "Green G (bits)"),
            ["BlueDepth"] = new("蓝色 B（bit）", "Blue B (bits)"),
            ["ColorCount"] = new("{0} 种颜色", "{0} colors"),
            ["EditPalette"] = new("编辑调色板", "Edit Palette"),
            ["BinaryThreshold"] = new("二值化阈值", "Binary Threshold"),
            ["DitherNone"] = new("无（普通阈值）", "None (Standard Threshold)"),
            ["DitherFloydSteinberg"] = new("Floyd–Steinberg", "Floyd–Steinberg"),
            ["DitherAtkinson"] = new("Atkinson", "Atkinson"),
            ["DitherJarvisJudiceNinke"] = new("Jarvis–Judice–Ninke", "Jarvis–Judice–Ninke"),
            ["DitherStucki"] = new("Stucki", "Stucki"),
            ["DitherBurkes"] = new("Burkes", "Burkes"),
            ["DitherSierraLite"] = new("Sierra Lite", "Sierra Lite"),
            ["InvertColors"] = new("颜色反相", "Invert Colors"),
            ["EnablePixelSpacing"] = new("启用像素间距", "Enable Pixel Spacing"),
            ["SpacingWidth"] = new("间距宽度", "Spacing Width"),
            ["SpacingColor"] = new("间距颜色", "Spacing Color"),
            ["CaptureSystemAudio"] = new(
                "录制系统当前输出的声音（WASAPI 回环）",
                "Record Current System Audio (WASAPI Loopback)"),
            ["StartRecording"] = new("开始录制 MP4", "Start MP4 Recording"),
            ["StopRecording"] = new("停止录制", "Stop Recording"),
            ["Pause"] = new("暂停", "Pause"),
            ["Resume"] = new("继续", "Resume"),
            ["SaveCurrentFrame"] = new("保存当前帧", "Save Current Frame"),
            ["SectionCaptureSource"] = new("抓取来源", "Capture Source"),
            ["SectionInputScaling"] = new("输入画面适配", "Input Scaling"),
            ["SectionOutputResolution"] = new("输出分辨率", "Output Resolution"),
            ["SectionPreviewScale"] = new("预览缩放", "Preview Scale"),
            ["SectionColorMode"] = new("颜色输出模式", "Color Output Mode"),
            ["SectionDithering"] = new("量化误差扩散算法", "Quantization Error Diffusion"),
            ["SectionPixelSpacing"] = new("液晶像素间距", "LCD Pixel Spacing"),
            ["SectionFrameRate"] = new("抓取与录制帧率", "Capture and Recording Frame Rate"),
            ["OriginalPreview"] = new("原画面", "Original"),
            ["OutputPreview"] = new("输出画面", "Output"),
            ["Ready"] = new("准备就绪", "Ready"),
            ["Paused"] = new("已暂停", "Paused"),
            ["Capturing"] = new("正在抓取", "Capturing"),
            ["SelectTargetWindow"] = new("请选择一个目标窗口", "Select a target window"),
            ["CurrentOutput"] = new("当前输出：{0} × {1}", "Current output: {0} × {1}"),
            ["CurrentOutputFollow"] = new("当前输出：跟随输入尺寸", "Current output: match input size"),
            ["CurrentOutputMultiplier"] = new("当前输出：输入尺寸的 {0}", "Current output: {0} of input size"),
            ["CaptureDimensions"] = new(
                "输入：{0} × {1}  量化：{2} × {3}  最终：{4} × {5}",
                "Input: {0} × {1}  Quantized: {2} × {3}  Final: {4} × {5}"),
            ["CaptureOutputDimensions"] = new(
                "输入：{0} × {1}  当前输出：{2} × {3}",
                "Input: {0} × {1}  Current output: {2} × {3}"),
            ["SourceWindow"] = new("窗口：{0}", "Window: {0}"),
            ["SourceDesktop"] = new("来源：整个桌面", "Source: entire desktop"),
            ["CaptureStatus"] = new("{0} | {1} | {2} × {3}", "{0} | {1} | {2} × {3}"),
            ["WaitForFirstFrame"] = new("请等待首帧画面生成后再开始录制。", "Wait for the first frame before recording."),
            ["Information"] = new("提示", "Information"),
            ["SaveQuantizedVideo"] = new("保存量化视频", "Save Quantized Video"),
            ["Mp4VideoFilter"] = new("MP4 视频|*.mp4", "MP4 Video|*.mp4"),
            ["RecordingPath"] = new("正在录制：{0}", "Recording: {0}"),
            ["CannotStartRecording"] = new("无法开始录制：{0}", "Could not start recording: {0}"),
            ["RecordingError"] = new("录制错误", "Recording Error"),
            ["FinalizingVideo"] = new("正在完成视频文件写入...", "Finalizing video file..."),
            ["RecordingCompleted"] = new("录制已完成", "Recording completed"),
            ["StopRecordingError"] = new("停止录制时发生错误：{0}", "An error occurred while stopping recording: {0}"),
            ["RecordingFailed"] = new("录制失败", "Recording failed"),
            ["RecordingActive"] = new("正在录制", "Recording"),
            ["FoundWindows"] = new("找到 {0} 个可抓取窗口", "Found {0} capturable windows"),
            ["ExportConfigurationTitle"] = new("导出色彩量化配置", "Export Color Quantization Settings"),
            ["ConfigurationSaveFilter"] = new(
                "色彩量化配置|*.dbconfig.json|JSON 文件|*.json",
                "Color Quantization Settings|*.dbconfig.json|JSON Files|*.json"),
            ["ConfigurationExported"] = new("配置已导出：{0}", "Settings exported: {0}"),
            ["CannotExportConfiguration"] = new("无法导出配置：{0}", "Could not export settings: {0}"),
            ["ConfigurationExportError"] = new("配置导出错误", "Settings Export Error"),
            ["StopBeforeImport"] = new("请先停止录制再导入配置。", "Stop recording before importing settings."),
            ["ImportConfigurationTitle"] = new("导入色彩量化配置", "Import Color Quantization Settings"),
            ["ConfigurationOpenFilter"] = new(
                "色彩量化配置|*.dbconfig.json;*.json|所有文件|*.*",
                "Color Quantization Settings|*.dbconfig.json;*.json|All Files|*.*"),
            ["ConfigurationImported"] = new("配置已导入：{0}", "Settings imported: {0}"),
            ["CannotImportConfiguration"] = new("无法导入配置：{0}", "Could not import settings: {0}"),
            ["ConfigurationImportError"] = new("配置导入错误", "Settings Import Error"),
            ["UnsupportedValue"] = new("不支持数值 {0}。", "Unsupported value: {0}."),
            ["NoFrameToSave"] = new("当前还没有可保存的画面。", "There is no frame to save yet."),
            ["SaveQuantizedImage"] = new("保存量化图像", "Save Quantized Image"),
            ["ImageSaveFilter"] = new("PNG 图像|*.png|BMP 图像|*.bmp", "PNG Image|*.png|BMP Image|*.bmp"),
            ["SavedPath"] = new("已保存：{0}", "Saved: {0}"),
            ["ColorDescriptionGrayscale"] = new("{0} bit 灰度", "{0}-bit grayscale"),
            ["ColorDescriptionRgbUniform"] = new("{0} bit RGB/通道", "{0}-bit RGB/channel"),
            ["ColorDescriptionRgbPerChannel"] = new("RGB {0}/{1}/{2} bit", "RGB {0}/{1}/{2} bits"),
            ["ColorDescriptionPalette"] = new("{0} 色调色板", "{0}-color palette"),
            ["PreferenceSaveError"] = new("无法保存语言偏好：{0}", "Could not save language preference: {0}"),
            ["PaletteEditorTitle"] = new("自定义调色板", "Custom Palette"),
            ["PaletteEditorDescription"] = new(
                "添加 2–256 种颜色，可从颜色对话框或屏幕像素取色。",
                "Add 2–256 colors using the color dialog or screen picker."),
            ["AddColor"] = new("添加颜色", "Add Color"),
            ["PickScreenColor"] = new("屏幕取色", "Pick from Screen"),
            ["EditColor"] = new("编辑颜色", "Edit Color"),
            ["DeleteColor"] = new("删除颜色", "Delete Color"),
            ["MoveUp"] = new("上移", "Move Up"),
            ["MoveDown"] = new("下移", "Move Down"),
            ["BlackAndWhite"] = new("黑白", "Black & White"),
            ["RgbEightColors"] = new("RGB 8色", "RGB 8 Colors"),
            ["Ok"] = new("确定", "OK"),
            ["Cancel"] = new("取消", "Cancel"),
            ["PaletteMaximum"] = new("调色板最多支持 256 种颜色。", "A palette can contain at most 256 colors."),
            ["PaletteMinimum"] = new("自定义调色板至少需要两种颜色。", "A custom palette requires at least two colors."),
            ["InvalidPalette"] = new("调色板无效", "Invalid Palette"),
            ["ScreenColorPicker"] = new("屏幕取色", "Screen Color Picker"),
            ["LeftClickSelect"] = new("左键选择", "Left-click to select"),
            ["RightClickCancel"] = new("右键 / Esc 取消", "Right-click / Esc to cancel"),
            ["NoColors"] = new("无颜色", "No colors"),
            ["WaitingForFrame"] = new("等待画面...", "Waiting for frame..."),
            ["PixelGridRequiresBitmap"] = new("像素网格预览需要位图图像。", "Pixel-grid preview requires a bitmap image."),
            ["ConfigurationEmpty"] = new("配置文件内容为空。", "The settings file is empty."),
            ["InvalidHexColor"] = new(
                "颜色“{0}”不是有效的 #RRGGBB 格式。",
                "Color \"{0}\" is not a valid #RRGGBB value."),
            ["UnsupportedConfigurationVersion"] = new(
                "不支持配置版本 {0}，当前版本为 {1}。",
                "Settings version {0} is not supported; the current version is {1}."),
            ["OutputWidth"] = new("输出宽度", "Output width"),
            ["OutputHeight"] = new("输出高度", "Output height"),
            ["OutputMultiplierName"] = new("输出倍数", "Output multiplier"),
            ["PreviewScaleName"] = new("预览缩放", "Preview scale"),
            ["GrayscaleDepthName"] = new("灰度位深", "Grayscale depth"),
            ["RgbDepthName"] = new("RGB 位深", "RGB depth"),
            ["RedDepthName"] = new("红色位深", "Red depth"),
            ["GreenDepthName"] = new("绿色位深", "Green depth"),
            ["BlueDepthName"] = new("蓝色位深", "Blue depth"),
            ["ThresholdName"] = new("二值化阈值", "Binary threshold"),
            ["PixelSpacingWidthName"] = new("像素间距宽度", "Pixel spacing width"),
            ["UnsupportedFrameRate"] = new("帧率 {0} 不在支持列表中。", "Frame rate {0} is not supported."),
            ["PaletteRange"] = new("调色板必须包含 2–256 种颜色。", "The palette must contain 2–256 colors."),
            ["SpacingColorInPalette"] = new("像素间距颜色必须存在于自定义调色板中。", "The pixel spacing color must exist in the custom palette."),
            ["UnsupportedEnum"] = new("{0} 包含不支持的值。", "{0} contains an unsupported value."),
            ["RangeError"] = new("{0}必须在 {1}–{2} 范围内。", "{0} must be between {1} and {2}."),
            ["UnsupportedSetting"] = new("{0} {1} 不受支持。", "{0} value {1} is not supported."),
            ["TargetWindowUnavailable"] = new("目标窗口已关闭或尺寸无效。", "The target window is closed or has an invalid size."),
            ["CannotGetDesktopContext"] = new("无法获取桌面设备上下文。", "Could not acquire the desktop device context."),
            ["DesktopCopyFailed"] = new("桌面画面复制失败。", "Desktop image copy failed."),
            ["RecordingAlreadyStarted"] = new("录制已经开始。", "Recording has already started."),
            ["FrameSizeMismatch"] = new(
                "录制帧尺寸必须为 {0} × {1}，当前为 {2} × {3}。",
                "Recording frames must be {0} × {1}; the current frame is {2} × {3}."),
            ["FfmpegNotFound"] = new(
                "未找到 FFmpeg。请运行 scripts/Setup-FFmpeg.ps1，或将 ffmpeg.exe 加入系统 PATH。",
                "FFmpeg was not found. Run scripts/Setup-FFmpeg.ps1 or add ffmpeg.exe to PATH."),
            ["FfmpegTimeout"] = new("FFmpeg 未能及时完成视频文件写入。", "FFmpeg did not finish writing the video file in time."),
            ["FfmpegRecordingFailed"] = new("FFmpeg 录制失败（退出码 {0}）：{1}", "FFmpeg recording failed (exit code {0}): {1}"),
            ["RecordingPathRequired"] = new("必须指定录制文件路径。", "A recording output path is required."),
            ["RecordingSizeRange"] = new("录制尺寸必须在 1 到 8192 之间。", "Recording dimensions must be between 1 and 8192."),
            ["RecordingFrameRateRange"] = new("录制帧率必须在 1 到 240 FPS 之间。", "Recording frame rate must be between 1 and 240 FPS."),
            ["CannotStartFfmpeg"] = new("无法启动 FFmpeg。", "Could not start FFmpeg."),
            ["UnsupportedAudioFormat"] = new("不支持系统音频格式：{0}", "Unsupported system audio format: {0}"),
            ["UnsupportedPcmFormat"] = new("不支持 {0} 位 PCM 系统音频。", "Unsupported {0}-bit PCM system audio."),
            ["OutputDimensionRange"] = new("输出宽高必须在 1 到 8192 之间。", "Output dimensions must be between 1 and 8192."),
            ["ThresholdRange"] = new("阈值必须在 0 到 255 之间。", "Threshold must be between 0 and 255."),
            ["UnsupportedColorMode"] = new("不支持所选的颜色量化模式。", "The selected color quantization mode is not supported."),
            ["PaletteMaximumArgument"] = new("自定义调色板最多支持 256 种颜色。", "A custom palette can contain at most 256 colors."),
            ["UnsupportedDither"] = new("不支持所选的扩散算法。", "The selected diffusion algorithm is not supported."),
            ["UnsupportedInputScaling"] = new("不支持所选的输入缩放方式。", "The selected input scaling mode is not supported."),
            ["PixelSpacingRange"] = new("像素间距宽度必须在 1 到 32 之间。", "Pixel spacing width must be between 1 and 32."),
            ["SpacedDimensionsTooLarge"] = new(
                "插入间距后的尺寸 {0} × {1} 超过 8192 × 8192。",
                "Dimensions after adding spacing ({0} × {1}) exceed 8192 × 8192."),
            ["ColorDepthRange"] = new("颜色位深必须在 1 到 8 bit 之间。", "Color depth must be between 1 and 8 bits."),
            ["CalculatedOutputOutOfRange"] = new(
                "计算出的输出尺寸 {0} × {1} 超出 1–{2} 范围。",
                "Calculated output dimensions {0} × {1} are outside the 1–{2} range."),
        };

    private static AppLanguage _language = AppLanguage.ChineseSimplified;

    public static AppLanguage Language
    {
        get => _language;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            _language = value;
        }
    }

    public static string Get(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (!Entries.TryGetValue(key, out Entry? entry))
        {
            throw new KeyNotFoundException($"Missing localization key: {key}");
        }

        return Language == AppLanguage.English ? entry.English : entry.Chinese;
    }

    public static string Format(string key, params object?[] arguments)
    {
        return string.Format(CultureInfo.CurrentCulture, Get(key), arguments);
    }
}
