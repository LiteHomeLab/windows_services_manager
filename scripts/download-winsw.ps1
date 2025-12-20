# WinSW Download Script for WinServiceManager
# Automatically downloads the correct WinSW version for the project

param(
    [string]$Version = "3.0.0",
    [string]$TargetDir = "src\WinServiceManager\templates",
    [switch]$Force
)

Write-Host "🚀 WinSW Download Script for WinServiceManager" -ForegroundColor Green
Write-Host "===========================================" -ForegroundColor Green

# Configuration
$WinSWVersion = $Version
$BaseUrl = "https://github.com/winsw/winsw/releases/download/v$WinSWVersion"
$FileName = "WinSW-x64.exe"
$DownloadUrl = "$BaseUrl/$FileName"
$TargetPath = Join-Path $TargetDir $FileName

Write-Host "Version: $WinSWVersion" -ForegroundColor Cyan
Write-Host "Target: $TargetPath" -ForegroundColor Cyan
Write-Host "URL: $DownloadUrl" -ForegroundColor Cyan

# Create target directory if it doesn't exist
if (-not (Test-Path $TargetDir)) {
    Write-Host "`n📁 Creating target directory..." -ForegroundColor Yellow
    New-Item -ItemType Directory -Path $TargetDir -Force | Out-Null
}

# Check if WinSW already exists
if ((Test-Path $TargetPath) -and -not $Force) {
    $existingFile = Get-Item $TargetPath
    Write-Host "`n✅ WinSW already exists: $($existingFile.Name) ($([math]::Round($existingFile.Length / 1MB, 2)) MB)" -ForegroundColor Green
    Write-Host "   Use -Force to override" -ForegroundColor Yellow
    exit 0
}

# Download WinSW
Write-Host "`n⬇️  Downloading WinSW..." -ForegroundColor Yellow

try {
    # Use Invoke-WebRequest for better progress tracking
    $webClient = New-Object System.Net.WebClient
    $webClient.DownloadProgressChanged = {
        param($sender, $e)
        $percentComplete = [math]::Round($e.ProgressPercentage, 2)
        $bytesReceived = [math]::Round($e.BytesReceived / 1MB, 2)
        $totalBytes = [math]::Round($e.TotalBytesToReceive / 1MB, 2)

        Write-Progress -Activity "Downloading WinSW" -Status "$bytesReceived MB / $totalBytes MB" -PercentComplete $percentComplete
    }

    $webClient.DownloadFile($DownloadUrl, $TargetPath)
    Write-Progress -Activity "Downloading WinSW" -Completed

    # Verify download
    if (Test-Path $TargetPath) {
        $downloadedFile = Get-Item $TargetPath
        Write-Host "✅ Download completed successfully!" -ForegroundColor Green
        Write-Host "   File: $($downloadedFile.FullName)" -ForegroundColor Cyan
        Write-Host "   Size: $([math]::Round($downloadedFile.Length / 1MB, 2)) MB" -ForegroundColor Cyan
        Write-Host "   Created: $($downloadedFile.CreationTime)" -ForegroundColor Cyan

        # Verify it's a valid executable
        try {
            $testResult = & $TargetPath --version 2>&1
            if ($LASTEXITCODE -eq 0 -or $testResult -match "WinSW") {
                Write-Host "✅ WinSW executable is valid and working" -ForegroundColor Green
            } else {
                Write-Host "⚠️  WinSW downloaded but may not be fully functional" -ForegroundColor Yellow
            }
        } catch {
            Write-Host "⚠️  Could not test WinSW executable: $_" -ForegroundColor Yellow
        }
    } else {
        throw "Download completed but file not found"
    }

} catch {
    Write-Host "❌ Download failed: $_" -ForegroundColor Red
    Write-Host "   Please check your internet connection and try again." -ForegroundColor Red
    Write-Host "   You can also download manually from: $DownloadUrl" -ForegroundColor Cyan
    exit 1
} finally {
    if ($webClient) {
        $webClient.Dispose()
    }
}

Write-Host "`n🎉 WinSW setup completed!" -ForegroundColor Green
Write-Host "The WinServiceManager is now ready to use." -ForegroundColor Green