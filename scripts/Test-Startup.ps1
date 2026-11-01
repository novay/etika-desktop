param(
    [Parameter(Mandatory = $true)][string]$PublishDirectory,
    [string]$DiagnosticsDirectory = "artifacts/startup-diagnostics"
)
$ErrorActionPreference = "Stop"
$publish = (Resolve-Path $PublishDirectory).Path
$diagnostics = New-Item -ItemType Directory -Force $DiagnosticsDirectory
$started = Get-Date
$process = $null
try {
    $arguments = @{
        FilePath = (Join-Path $publish "Etika.Desktop.exe")
        WorkingDirectory = $publish
        PassThru = $true
        RedirectStandardOutput = (Join-Path $diagnostics.FullName "stdout.log")
        RedirectStandardError = (Join-Path $diagnostics.FullName "stderr.log")
    }
    $process = Start-Process @arguments
    $hasWindow = $false
    for ($i = 0; $i -lt 60; $i++) {
        Start-Sleep -Milliseconds 250
        $process.Refresh()
        if ($process.HasExited) {
            throw ("ETIKA exited during startup. Exit code: {0} (0x{1:X8})" -f $process.ExitCode, $process.ExitCode)
        }
        if ($process.MainWindowHandle -ne 0 -and $process.MainWindowTitle -eq "ETIKA Desktop") {
            $hasWindow = $true
            break
        }
    }
    if (!$hasWindow) { throw "Process started but no main window appeared within 15 seconds." }
    Start-Sleep -Seconds 3
    $process.Refresh()
    if ($process.HasExited) { throw "ETIKA exited after creating the main window." }
    Write-Host "PASS: ETIKA main window opened and remained running."
}
finally {
    $appLog = Join-Path $env:LOCALAPPDATA "ETIKA/Desktop/Logs/startup.log"
    if (Test-Path $appLog) { Copy-Item $appLog (Join-Path $diagnostics.FullName "startup.log") }
    Get-WinEvent -FilterHashtable @{ LogName = 'Application'; StartTime = $started } -ErrorAction SilentlyContinue |
        Where-Object { $_.Message -match 'Etika.Desktop' } |
        Format-List TimeCreated, ProviderName, Id, Message |
        Out-File (Join-Path $diagnostics.FullName "windows-events.log")
    if ($null -ne $process -and !$process.HasExited) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    }
    Get-ChildItem $diagnostics.FullName -Filter '*.log' | ForEach-Object {
        Write-Host ("--- " + $_.Name)
        Get-Content $_.FullName
    }
}
