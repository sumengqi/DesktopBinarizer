using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using NAudio.Wave;

namespace DesktopBinarizer.Recording;

public sealed class VideoRecorder : IAsyncDisposable
{
    private static readonly Guid PcmSubFormat =
        new("00000001-0000-0010-8000-00aa00389b71");
    private static readonly Guid FloatSubFormat =
        new("00000003-0000-0010-8000-00aa00389b71");

    private readonly object _frameLock = new();
    private NamedPipeServerStream? _videoPipe;
    private NamedPipeServerStream? _audioPipe;
    private WasapiLoopbackCapture? _audioCapture;
    private BufferedWaveProvider? _audioBuffer;
    private CancellationTokenSource? _videoCancellation;
    private CancellationTokenSource? _audioCancellation;
    private Task? _videoWriterTask;
    private Task? _audioWriterTask;
    private Process? _ffmpegProcess;
    private Task<string>? _ffmpegErrorTask;
    private Stopwatch? _stopwatch;
    private byte[]? _latestFrame;
    private int _width;
    private int _height;
    private int _framesPerSecond;
    private bool _isStopping;

    public bool IsRecording { get; private set; }

    public TimeSpan Elapsed => _stopwatch?.Elapsed ?? TimeSpan.Zero;

    public async Task StartAsync(RecordingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (IsRecording)
        {
            throw new InvalidOperationException("录制已经开始。");
        }

        ValidateOptions(options);

        string ffmpegPath = ResolveFfmpegPath();

        string? outputDirectory = Path.GetDirectoryName(options.OutputPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        _width = options.Width;
        _height = options.Height;
        _framesPerSecond = options.FramesPerSecond;
        _latestFrame = new byte[_width * _height * 4];
        _isStopping = false;

        string suffix = $"{Environment.ProcessId}-{Guid.NewGuid():N}";
        string videoPipeName = $"desktop-binarizer-video-{suffix}";
        string? audioPipeName = options.CaptureSystemAudio
            ? $"desktop-binarizer-audio-{suffix}"
            : null;

        _videoPipe = CreatePipe(videoPipeName);
        Task videoConnection = _videoPipe.WaitForConnectionAsync();

        WaveFormat? audioFormat = null;
        Task? audioConnection = null;
        if (options.CaptureSystemAudio)
        {
            _audioCapture = new WasapiLoopbackCapture();
            audioFormat = _audioCapture.WaveFormat;
            _audioBuffer = new BufferedWaveProvider(audioFormat)
            {
                BufferDuration = TimeSpan.FromSeconds(2),
                DiscardOnBufferOverflow = true,
                ReadFully = true,
            };
            _audioPipe = CreatePipe(audioPipeName!);
            audioConnection = _audioPipe.WaitForConnectionAsync();
            _audioCapture.DataAvailable += AudioCaptureOnDataAvailable;
        }

        try
        {
            _ffmpegProcess = StartFfmpeg(
                ffmpegPath,
                options,
                videoPipeName,
                audioPipeName,
                audioFormat);
            _ffmpegErrorTask = _ffmpegProcess.StandardError.ReadToEndAsync();

            using var connectionTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await videoConnection.WaitAsync(connectionTimeout.Token);

            _videoCancellation = new CancellationTokenSource();
            _videoWriterTask = WriteVideoAsync(
                _videoPipe,
                _videoCancellation.Token);

            if (audioConnection is not null)
            {
                await audioConnection.WaitAsync(connectionTimeout.Token);
            }

            if (_audioCapture is not null &&
                _audioPipe is not null &&
                _audioBuffer is not null &&
                audioFormat is not null)
            {
                _audioCancellation = new CancellationTokenSource();
                _audioWriterTask = WriteAudioAsync(
                    _audioPipe,
                    _audioBuffer,
                    audioFormat,
                    _audioCancellation.Token);
                _audioCapture.StartRecording();
            }

            _stopwatch = Stopwatch.StartNew();
            IsRecording = true;
        }
        catch
        {
            await AbortAsync();
            throw;
        }
    }

    public void SubmitFrame(Bitmap frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (!IsRecording || _isStopping)
        {
            return;
        }

        if (frame.Width != _width || frame.Height != _height)
        {
            throw new InvalidOperationException(
                $"录制帧尺寸必须为 {_width} × {_height}，当前为 {frame.Width} × {frame.Height}。");
        }

        byte[] pixels = CopyBgraPixels(frame);
        lock (_frameLock)
        {
            _latestFrame = pixels;
        }
    }

    private static string ResolveFfmpegPath()
    {
        string bundledPath = Path.Combine(
            AppContext.BaseDirectory,
            "ffmpeg",
            "ffmpeg.exe");
        if (File.Exists(bundledPath))
        {
            return bundledPath;
        }

        string? pathValue = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrWhiteSpace(pathValue))
        {
            foreach (string pathEntry in pathValue.Split(Path.PathSeparator))
            {
                string directory = pathEntry.Trim().Trim('"');
                if (string.IsNullOrWhiteSpace(directory))
                {
                    continue;
                }

                string candidate = Path.Combine(directory, "ffmpeg.exe");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        throw new FileNotFoundException(
            "未找到 FFmpeg。请运行 scripts/Setup-FFmpeg.ps1，" +
            "或将 ffmpeg.exe 加入系统 PATH。",
            bundledPath);
    }

    public async Task StopAsync()
    {
        if (!IsRecording || _isStopping)
        {
            return;
        }

        _isStopping = true;
        IsRecording = false;
        _stopwatch?.Stop();

        if (_audioCapture is not null)
        {
            _audioCapture.StopRecording();
        }

        _videoCancellation?.Cancel();
        _audioCancellation?.Cancel();

        await IgnoreCancellationAsync(_videoWriterTask);
        await IgnoreCancellationAsync(_audioWriterTask);

        _videoPipe?.Dispose();
        _audioPipe?.Dispose();

        try
        {
            string errorOutput = string.Empty;
            if (_ffmpegProcess is not null)
            {
                using var exitTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                try
                {
                    await _ffmpegProcess.WaitForExitAsync(exitTimeout.Token);
                }
                catch (OperationCanceledException)
                {
                    _ffmpegProcess.Kill(entireProcessTree: true);
                    await _ffmpegProcess.WaitForExitAsync();
                    throw new TimeoutException("FFmpeg 未能及时完成视频文件写入。");
                }

                if (_ffmpegErrorTask is not null)
                {
                    errorOutput = await _ffmpegErrorTask;
                }

                if (_ffmpegProcess.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        $"FFmpeg 录制失败（退出码 {_ffmpegProcess.ExitCode}）：{errorOutput.Trim()}");
                }
            }
        }
        finally
        {
            DisposeResources();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (IsRecording)
        {
            await StopAsync();
        }

        DisposeResources();
        GC.SuppressFinalize(this);
    }

    private static void ValidateOptions(RecordingOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.OutputPath))
        {
            throw new ArgumentException("必须指定录制文件路径。", nameof(options));
        }

        if (options.Width is < 1 or > 8192 ||
            options.Height is < 1 or > 8192)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "录制尺寸必须在 1 到 8192 之间。");
        }

        if (options.FramesPerSecond is < 1 or > 240)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "录制帧率必须在 1 到 240 FPS 之间。");
        }
    }

    private static NamedPipeServerStream CreatePipe(string pipeName)
    {
        return new NamedPipeServerStream(
            pipeName,
            PipeDirection.Out,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            1024 * 1024,
            1024 * 1024);
    }

    private static Process StartFfmpeg(
        string ffmpegPath,
        RecordingOptions options,
        string videoPipeName,
        string? audioPipeName,
        WaveFormat? audioFormat)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = false,
        };

        startInfo.ArgumentList.Add("-hide_banner");
        startInfo.ArgumentList.Add("-loglevel");
        startInfo.ArgumentList.Add("error");
        startInfo.ArgumentList.Add("-y");
        startInfo.ArgumentList.Add("-f");
        startInfo.ArgumentList.Add("rawvideo");
        startInfo.ArgumentList.Add("-pixel_format");
        startInfo.ArgumentList.Add("bgra");
        startInfo.ArgumentList.Add("-video_size");
        startInfo.ArgumentList.Add($"{options.Width}x{options.Height}");
        startInfo.ArgumentList.Add("-framerate");
        startInfo.ArgumentList.Add(options.FramesPerSecond.ToString());
        startInfo.ArgumentList.Add("-i");
        startInfo.ArgumentList.Add(GetPipePath(videoPipeName));

        if (audioPipeName is not null && audioFormat is not null)
        {
            startInfo.ArgumentList.Add("-f");
            startInfo.ArgumentList.Add(GetAudioInputFormat(audioFormat));
            startInfo.ArgumentList.Add("-ar");
            startInfo.ArgumentList.Add(audioFormat.SampleRate.ToString());
            startInfo.ArgumentList.Add("-ac");
            startInfo.ArgumentList.Add(audioFormat.Channels.ToString());
            startInfo.ArgumentList.Add("-i");
            startInfo.ArgumentList.Add(GetPipePath(audioPipeName));
        }

        startInfo.ArgumentList.Add("-map");
        startInfo.ArgumentList.Add("0:v:0");
        if (audioPipeName is not null)
        {
            startInfo.ArgumentList.Add("-map");
            startInfo.ArgumentList.Add("1:a:0");
        }

        startInfo.ArgumentList.Add("-c:v");
        startInfo.ArgumentList.Add("libx264");
        startInfo.ArgumentList.Add("-preset");
        startInfo.ArgumentList.Add("ultrafast");
        startInfo.ArgumentList.Add("-tune");
        startInfo.ArgumentList.Add("zerolatency");
        startInfo.ArgumentList.Add("-crf");
        startInfo.ArgumentList.Add("18");
        startInfo.ArgumentList.Add("-vf");
        startInfo.ArgumentList.Add("pad=ceil(iw/2)*2:ceil(ih/2)*2");
        startInfo.ArgumentList.Add("-pix_fmt");
        startInfo.ArgumentList.Add("yuv420p");

        if (audioPipeName is not null)
        {
            startInfo.ArgumentList.Add("-c:a");
            startInfo.ArgumentList.Add("aac");
            startInfo.ArgumentList.Add("-b:a");
            startInfo.ArgumentList.Add("192k");
            startInfo.ArgumentList.Add("-shortest");
        }

        startInfo.ArgumentList.Add("-movflags");
        startInfo.ArgumentList.Add("+faststart");
        startInfo.ArgumentList.Add(options.OutputPath);

        return Process.Start(startInfo)
            ?? throw new InvalidOperationException("无法启动 FFmpeg。");
    }

    private static string GetAudioInputFormat(WaveFormat format)
    {
        if (format.Encoding == WaveFormatEncoding.IeeeFloat)
        {
            return format.BitsPerSample == 64 ? "f64le" : "f32le";
        }

        if (format.Encoding == WaveFormatEncoding.Pcm)
        {
            return GetPcmFormat(format.BitsPerSample);
        }

        if (format.Encoding == WaveFormatEncoding.Extensible &&
            format is WaveFormatExtensible extensible)
        {
            if (extensible.SubFormat == FloatSubFormat)
            {
                return format.BitsPerSample == 64 ? "f64le" : "f32le";
            }

            if (extensible.SubFormat == PcmSubFormat)
            {
                return GetPcmFormat(format.BitsPerSample);
            }
        }

        throw new NotSupportedException($"不支持系统音频格式：{format}");
    }

    private static string GetPcmFormat(int bitsPerSample)
    {
        return bitsPerSample switch
        {
            8 => "u8",
            16 => "s16le",
            24 => "s24le",
            32 => "s32le",
            _ => throw new NotSupportedException(
                $"不支持 {bitsPerSample} 位 PCM 系统音频。"),
        };
    }

    private static string GetPipePath(string pipeName)
    {
        return $@"\\.\pipe\{pipeName}";
    }

    private async Task WriteVideoAsync(
        NamedPipeServerStream pipe,
        CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(
            TimeSpan.FromSeconds(1.0 / _framesPerSecond));

        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            byte[] frame;
            lock (_frameLock)
            {
                frame = _latestFrame!;
            }

            await pipe.WriteAsync(frame, cancellationToken);
        }
    }

    private static async Task WriteAudioAsync(
        NamedPipeServerStream pipe,
        BufferedWaveProvider buffer,
        WaveFormat format,
        CancellationToken cancellationToken)
    {
        const int chunksPerSecond = 50;
        int bytesPerChunk = Math.Max(
            format.BlockAlign,
            format.AverageBytesPerSecond / chunksPerSecond);
        bytesPerChunk -= bytesPerChunk % format.BlockAlign;
        byte[] audioChunk = new byte[bytesPerChunk];
        using var timer = new PeriodicTimer(
            TimeSpan.FromSeconds(1.0 / chunksPerSecond));

        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            buffer.Read(audioChunk, 0, audioChunk.Length);
            await pipe.WriteAsync(audioChunk, cancellationToken);
        }
    }

    private void AudioCaptureOnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (_audioBuffer is null || _isStopping)
        {
            return;
        }

        _audioBuffer.AddSamples(e.Buffer, 0, e.BytesRecorded);
    }

    private static byte[] CopyBgraPixels(Bitmap frame)
    {
        Rectangle bounds = new(0, 0, frame.Width, frame.Height);
        BitmapData data = frame.LockBits(
            bounds,
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppArgb);

        try
        {
            int rowBytes = frame.Width * 4;
            byte[] pixels = new byte[rowBytes * frame.Height];
            if (data.Stride == rowBytes)
            {
                Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
                return pixels;
            }

            for (int y = 0; y < frame.Height; y++)
            {
                nint sourceRow = data.Scan0 + (y * data.Stride);
                Marshal.Copy(sourceRow, pixels, y * rowBytes, rowBytes);
            }

            return pixels;
        }
        finally
        {
            frame.UnlockBits(data);
        }
    }

    private static async Task IgnoreCancellationAsync(Task? task)
    {
        if (task is null)
        {
            return;
        }

        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
        }
        catch (IOException)
        {
        }
    }

    private async Task AbortAsync()
    {
        _isStopping = true;
        _videoCancellation?.Cancel();
        _audioCancellation?.Cancel();
        _audioCapture?.StopRecording();
        _videoPipe?.Dispose();
        _audioPipe?.Dispose();

        if (_ffmpegProcess is { HasExited: false })
        {
            _ffmpegProcess.Kill(entireProcessTree: true);
            await _ffmpegProcess.WaitForExitAsync();
        }

        DisposeResources();
    }

    private void DisposeResources()
    {
        if (_audioCapture is not null)
        {
            _audioCapture.DataAvailable -= AudioCaptureOnDataAvailable;
            _audioCapture.Dispose();
        }

        _videoCancellation?.Dispose();
        _audioCancellation?.Dispose();
        _videoPipe?.Dispose();
        _audioPipe?.Dispose();
        _ffmpegProcess?.Dispose();

        _audioCapture = null;
        _audioBuffer = null;
        _videoCancellation = null;
        _audioCancellation = null;
        _videoPipe = null;
        _audioPipe = null;
        _ffmpegProcess = null;
        _ffmpegErrorTask = null;
        _videoWriterTask = null;
        _audioWriterTask = null;
        _stopwatch = null;
        _latestFrame = null;
        _isStopping = false;
        IsRecording = false;
    }
}
