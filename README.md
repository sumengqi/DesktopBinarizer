# Desktop Binarizer

[![CI](https://github.com/sumengqi/DesktopBinarizer/actions/workflows/ci.yml/badge.svg)](https://github.com/sumengqi/DesktopBinarizer/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

一个用于 Windows 的实时桌面色彩量化工具。它可以抓取整个虚拟桌面或指定窗口，
将画面缩放到目标分辨率，再进行灰度、低位深 RGB 或自定义调色板量化。

> 平台：Windows 10/11 x64 · 运行时：.NET 8 Desktop Runtime

## 功能

- 抓取包含多显示器的整个虚拟桌面
- 枚举并抓取指定顶层窗口
- 输入画面可按比例完整缩放，或拉伸铺满整个输出
- 输出尺寸支持自定义、跟随输入和输入尺寸倍数
- 自定义分辨率支持逐位键入，编辑结束后自动限制为至少 `16 × 16`
- 支持将全部画面处理、预览和录制选项导出或导入为 JSON 配置
- 预览倍率通过一个下拉框选择：适应窗口、0.125×–8×
- 可同时显示原画面和量化后的输出画面，便于实时对比
- 灰度输出支持 `1–8 bit`，其中 `1 bit` 即二值化
- RGB 等位深支持每个通道统一设置 `1–8 bit`
- RGB 分通道位深支持 R/G/B 分别设置 `1–8 bit`
- 自定义调色板支持 `2–256` 种任意颜色
- 调色板编辑器支持添加、编辑、删除、排序、快捷预设和全屏吸管取色
- 可在逻辑像素之间插入 `1–32 px` 间距，模拟真实液晶像素网格
- 预览倍率先放大每个逻辑输出像素，间距只绘制在原输出分辨率的像素边界
- 例如 `192 × 64` 放大 `2×` 时，每个逻辑像素为 `2 × 2` 色块，再插入固定宽度间距
- 像素网格采用点对点最近邻绘制，不使用抗锯齿，也不添加外围边框
- 像素间距颜色可从自定义调色板中选择
- 1 bit 灰度可调二值化阈值
- 所有颜色模式均支持无抖动或六种误差扩散算法：
  Floyd–Steinberg、Atkinson、Jarvis–Judice–Ninke、Stucki、Burkes、Sierra Lite
- 颜色反相
- 5–240 FPS 抓取与录制帧率
- 保存当前量化结果为 PNG 或 BMP
- 将量化画面录制为 H.264 MP4
- 通过 WASAPI Loopback 同步录制系统当前播放的声音
- 对 QQ 音乐硬件合成窗口抑制单帧缺层闪烁，指定窗口和全桌面抓取均生效

## 快速开始

需要 Windows 和 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) 或更高版本。

```powershell
.\scripts\Setup-FFmpeg.ps1
dotnet build DesktopBinarizer.sln
dotnet run --project src/DesktopBinarizer/DesktopBinarizer.csproj
```

FFmpeg 不直接存放在 Git 仓库中。上述脚本会下载固定版本的 Windows Essentials
构建并校验 SHA-256。如果系统 `PATH` 中已有 `ffmpeg.exe`，应用也可直接使用它。

## 测试

项目包含不依赖第三方测试框架的冒烟测试：

```powershell
dotnet run --project tests/DesktopBinarizer.SmokeTests/DesktopBinarizer.SmokeTests.csproj
```

系统音频回环测试是可选的，需要当前默认播放设备可用：

```powershell
$env:RUN_AUDIO_LOOPBACK_TEST = "1"
dotnet run --project tests/DesktopBinarizer.SmokeTests/DesktopBinarizer.SmokeTests.csproj
```

GitHub Actions 会在 Windows 环境中自动构建项目并运行常规冒烟测试。

## 发布

普通发布（需要目标电脑安装 .NET 8 Desktop Runtime）：

```powershell
.\scripts\Setup-FFmpeg.ps1
dotnet publish src/DesktopBinarizer/DesktopBinarizer.csproj `
  -c Release -o publish
```

运行 `publish/DesktopBinarizer.exe`，同目录的 DLL 和配置文件需要一起保留。
录制依赖的 `ffmpeg` 子目录也必须一同保留。

## 项目结构

```text
src/DesktopBinarizer/                  WinForms 应用程序
tests/DesktopBinarizer.SmokeTests/     功能与回归冒烟测试
scripts/Setup-FFmpeg.ps1               FFmpeg 下载与校验脚本
tools/ffmpeg/                          FFmpeg 依赖说明与本地生成目录
.github/workflows/ci.yml               Windows 构建与测试流程
```

## 说明

部分使用硬件加速或受保护内容的窗口可能不支持 `PrintWindow`，程序会在该 API
失败时回退到屏幕区域复制。回退模式只能抓到屏幕上当前可见的内容。

录制使用 NAudio 进行 WASAPI 系统音频回环采集，并使用 FFmpeg 进行 H.264/AAC
编码。FFmpeg 的许可证与构建说明位于发布目录的 `ffmpeg` 子目录。
第三方组件说明见 `THIRD_PARTY_NOTICES.md`。
项目变更记录见 [CHANGELOG.md](CHANGELOG.md)。

## 开源协议

本项目源码使用 [MIT License](LICENSE)。FFmpeg 和 NAudio 仍分别适用各自的许可条款，
详见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。
