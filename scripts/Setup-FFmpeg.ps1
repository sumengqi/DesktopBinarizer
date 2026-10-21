[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$ffmpegVersion = '8.1.2'
$archiveName = "ffmpeg-$ffmpegVersion-essentials_build.zip"
$archiveUri = "https://www.gyan.dev/ffmpeg/builds/packages/$archiveName"
$expectedSha256 = 'db580001caa24ac104c8cb856cd113a87b0a443f7bdf47d8c12b1d740584a2ec'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$destinationDirectory = Join-Path $repositoryRoot 'tools\ffmpeg\win-x64'
$destinationExecutable = Join-Path $destinationDirectory 'ffmpeg.exe'

if (Test-Path -LiteralPath $destinationExecutable) {
    $versionLine = & $destinationExecutable -version 2>$null | Select-Object -First 1
    if ($versionLine -match "ffmpeg version $([regex]::Escape($ffmpegVersion))") {
        Write-Host "FFmpeg $ffmpegVersion is already installed."
        exit 0
    }
}

$temporaryDirectory = Join-Path `
    ([System.IO.Path]::GetTempPath()) `
    "desktop-binarizer-ffmpeg-$([guid]::NewGuid().ToString('N'))"
$archivePath = Join-Path $temporaryDirectory $archiveName
$extractDirectory = Join-Path $temporaryDirectory 'extracted'

try {
    New-Item -ItemType Directory -Path $temporaryDirectory | Out-Null
    Write-Host "Downloading FFmpeg $ffmpegVersion..."
    Invoke-WebRequest -Uri $archiveUri -OutFile $archivePath

    $actualSha256 = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualSha256 -ne $expectedSha256) {
        throw "FFmpeg archive checksum mismatch. Expected $expectedSha256, got $actualSha256."
    }

    Expand-Archive -LiteralPath $archivePath -DestinationPath $extractDirectory
    $packageDirectory = Get-ChildItem -LiteralPath $extractDirectory -Directory |
        Select-Object -First 1
    if ($null -eq $packageDirectory) {
        throw 'The FFmpeg archive did not contain the expected package directory.'
    }

    $sourceExecutable = Join-Path $packageDirectory.FullName 'bin\ffmpeg.exe'
    $sourceLicense = Join-Path $packageDirectory.FullName 'LICENSE'
    $sourceReadme = Join-Path $packageDirectory.FullName 'README.txt'
    foreach ($requiredPath in @($sourceExecutable, $sourceLicense, $sourceReadme)) {
        if (-not (Test-Path -LiteralPath $requiredPath)) {
            throw "The FFmpeg archive is missing: $requiredPath"
        }
    }

    New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null
    Copy-Item -LiteralPath $sourceExecutable -Destination $destinationExecutable -Force
    Copy-Item -LiteralPath $sourceLicense -Destination (Join-Path $destinationDirectory 'LICENSE') -Force
    Copy-Item -LiteralPath $sourceReadme -Destination (Join-Path $destinationDirectory 'README.txt') -Force
    Write-Host "Installed FFmpeg $ffmpegVersion to $destinationDirectory"
}
finally {
    if (Test-Path -LiteralPath $temporaryDirectory) {
        Remove-Item -LiteralPath $temporaryDirectory -Recurse -Force
    }
}
