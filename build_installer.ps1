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

# Compile Inno Setup Installer
Write-Host "Compiling Inno Setup Installer..."
$isccPath = "C:\Program Files\Inno Setup 7\ISCC.exe"
if (-not (Test-Path $isccPath)) {
    $isccPath = (Get-ChildItem "C:\Program Files*\Inno Setup*" -Recurse -Filter "ISCC.exe" -ErrorAction SilentlyContinue | Select-Object -First 1).FullName
}
if ($isccPath -and (Test-Path $isccPath)) {
    & $isccPath "installer.iss"
} else {
    Write-Warning "ISCC.exe not found! Please ensure Inno Setup is installed."
}

# Create Clean Zip Bundle containing only: Setup.exe, extension folder, and register .bat
Write-Host "Assembling Clean Release Bundle..."
$bundleDir = "ZipBundle"
if (Test-Path $bundleDir) { Remove-Item -Recurse -Force $bundleDir }
New-Item -ItemType Directory -Force -Path $bundleDir | Out-Null

Copy-Item "ReleaseBundle\FoxLoader_Setup_v1.0.exe" -Destination "$bundleDir\" -Force
Copy-Item -Path "extension" -Destination "$bundleDir\extension" -Recurse -Force
Copy-Item -Path "$OutputDir\register_browser_integration.bat" -Destination "$bundleDir\" -Force

# Zip Final Release Bundle
Write-Host "Zipping Final Clean Release Bundle..."
if (Get-Command 7z -ErrorAction SilentlyContinue) {
    7z a -tzip -mx=9 $ZipName ".\$bundleDir\*" | Out-Null
} else {
    Compress-Archive -Path "$bundleDir\*" -DestinationPath $ZipName -Force
}

Remove-Item -Recurse -Force $bundleDir -ErrorAction SilentlyContinue

Write-Host "Done! Output is $ZipName"

