$ErrorActionPreference = "Stop"

$OutputDir = "ReleaseOutput"
$ZipName = "FoxLoader_v1.0_Windows_x64.zip"

Write-Host "Cleaning up old release..."
if (Test-Path $OutputDir) { Remove-Item -Recurse -Force $OutputDir }
if (Test-Path $ZipName) { Remove-Item -Force $ZipName }

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

Write-Host "Copying Windows UI..."
Copy-Item -Path "windows\bin\Release\net8.0-windows10.0.26100.0\win-x64\publish\*" -Destination $OutputDir -Recurse -Force

Write-Host "Copying Rust Core..."
Copy-Item -Path "target\release\ermiya-core.exe" -Destination $OutputDir -Force

Write-Host "Copying Assets & Manifests..."
Copy-Item -Path "logo.png" -Destination $OutputDir -Force
Copy-Item -Path "windows\native_host_manifest.json" -Destination $OutputDir -Force

# Create convenient native messaging registration script
$registerScript = @"
@echo off
set MANIFEST_PATH=%~dp0native_host_manifest.json
echo Registering FoxLoader Native Messaging Host...
reg add "HKCU\Software\Google\Chrome\NativeMessagingHosts\com.ermiya.downloadmanager" /ve /t REG_SZ /d "%MANIFEST_PATH%" /f
reg add "HKCU\Software\Microsoft\Edge\NativeMessagingHosts\com.ermiya.downloadmanager" /ve /t REG_SZ /d "%MANIFEST_PATH%" /f
reg add "HKCU\Software\BraveSoftware\Brave-Browser\NativeMessagingHosts\com.ermiya.downloadmanager" /ve /t REG_SZ /d "%MANIFEST_PATH%" /f
echo Registered successfully!
pause
"@
Set-Content -Path "$OutputDir\register_browser_integration.bat" -Value $registerScript

Write-Host "Copying Extension..."
Copy-Item -Path "extension" -Destination "$OutputDir\extension" -Recurse -Force

Write-Host "Zipping Extension for Chrome Web Store..."
Compress-Archive -Path "extension\*" -DestinationPath "$OutputDir\extension.zip" -Force

Write-Host "Zipping Final Release Bundle..."
Compress-Archive -Path "$OutputDir\*" -DestinationPath $ZipName -Force

Write-Host "Done! Output is $ZipName"

