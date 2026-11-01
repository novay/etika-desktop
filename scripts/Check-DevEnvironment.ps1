$ErrorActionPreference = 'Stop'
if (![System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Windows)) {
    throw 'Jalankan pemeriksaan ini di Windows tempat aplikasi akan dikembangkan.'
}
if (!(Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'Pasang .NET SDK 10 melalui Visual Studio Installer.' }
$sdks = & dotnet --list-sdks
if (!($sdks | Where-Object { $_ -match '^10\.' })) { throw '.NET SDK 10 belum ditemukan.' }
$architecture = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture
$platform = if ($architecture -eq 'Arm64') { 'ARM64' } else { 'x64' }
Write-Host "PASS: .NET SDK 10 tersedia. Platform solution yang sesuai: $platform."
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
if (!(Test-Path $vswhere)) { throw 'Visual Studio Installer belum ditemukan. Pasang Visual Studio 2026 dan workload WinUI application development.' }
$installation = & $vswhere -latest -products '*' -property installationPath
if (!$installation) { throw 'Visual Studio belum ditemukan.' }
Write-Host "Visual Studio: $installation"
Write-Host 'Pastikan workload WinUI application development aktif pada Visual Studio Installer.'
Write-Host "Buka Etika.Desktop.sln; pilih Debug / $platform / ETIKA UI Preview, lalu F5."
Write-Host 'Periksa Tools > Options > Debugging > XAML Hot Reload bila perubahan XAML belum diterapkan.'
