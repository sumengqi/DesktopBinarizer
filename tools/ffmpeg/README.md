# FFmpeg dependency

The FFmpeg executable is intentionally not stored in this Git repository.
Run the setup script from the repository root to download the pinned Windows
Essentials build and verify its SHA-256 checksum:

```powershell
.\scripts\Setup-FFmpeg.ps1
```

The generated `win-x64` directory is ignored by Git. Its contents are copied
into build and publish output by `DesktopBinarizer.csproj`.

The application can also use an `ffmpeg.exe` available on the system `PATH`.
See the repository's `THIRD_PARTY_NOTICES.md` for licensing information.
