param(
    [Parameter(Mandatory = $true)][string]$PublishDirectory,
    [string]$DiagnosticsDirectory = "artifacts/workspace-preview",
    [switch]$PreviewMustBeDisabled
)
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class EtikaPreviewWindow {
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr window, IntPtr dc, uint flags);
}
'@
$publish = (Resolve-Path $PublishDirectory).Path
$diagnostics = New-Item -ItemType Directory -Force $DiagnosticsDirectory
$oldPreview = $env:ETIKA_UI_PREVIEW

function Find-ById($root, [string]$id) {
    return $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
        [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $id))
}
function Wait-Element($root, [string]$id) {
    for ($i = 0; $i -lt 60; $i++) {
        $element = Find-ById $root $id
        if ($null -ne $element -and !$element.Current.IsOffscreen) { return $element }
        Start-Sleep -Milliseconds 200
    }
    throw "Visible UI element did not appear: $id"
}
function Save-Window($process, [string]$name) {
    $rect = [EtikaPreviewWindow+Rect]::new()
    [void][EtikaPreviewWindow]::GetWindowRect($process.MainWindowHandle, [ref]$rect)
    $bitmap = [System.Drawing.Bitmap]::new($rect.Right - $rect.Left, $rect.Bottom - $rect.Top)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $dc = $graphics.GetHdc()
    try { $captured = [EtikaPreviewWindow]::PrintWindow($process.MainWindowHandle, $dc, 2) }
    finally { $graphics.ReleaseHdc($dc) }
    try {
        if (!$captured) { throw "PrintWindow failed for $name" }
        $bitmap.Save((Join-Path $diagnostics.FullName "$name.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally { $graphics.Dispose(); $bitmap.Dispose() }
}
$cases = @(
    @{ Role = 'Admin'; Menus = @('all', 'berkas', 'tapd') },
    @{ Role = 'Eksekutif'; Menus = @('all') },
    @{ Role = 'SKPD'; Menus = @('own') },
    @{ Role = 'Berkas'; Menus = @('berkas') },
    @{ Role = 'TAPD'; Menus = @('tapd') },
    @{ Role = 'SKPD|TAPD'; Menus = @('accessible', 'tapd') }
)
if ($PreviewMustBeDisabled) { $cases = @($cases[0]) }
try {
    foreach ($case in $cases) {
        $process = $null
        try {
            $env:ETIKA_UI_PREVIEW = $case.Role
            $process = Start-Process -FilePath (Join-Path $publish 'Etika.Desktop.exe') -WorkingDirectory $publish -PassThru
            for ($i = 0; $i -lt 60; $i++) {
                $process.Refresh()
                if ($process.HasExited) { throw "Preview exited: $($case.Role), code $($process.ExitCode)" }
                if ($process.MainWindowHandle -ne 0) { break }
                Start-Sleep -Milliseconds 250
            }
            $root = [System.Windows.Automation.AutomationElement]::FromHandle($process.MainWindowHandle)
            if ($PreviewMustBeDisabled) {
                [void](Wait-Element $root 'LoginButton')
                if ($null -ne (Find-ById $root 'Nav_all')) { throw 'Release allowed debug preview navigation.' }
                Write-Host 'PASS: Release ignores ETIKA_UI_PREVIEW and displays login.'
                continue
            }
            [void](Wait-Element $root ('Nav_' + $case.Menus[0]))
            foreach ($key in @('all', 'own', 'accessible', 'berkas', 'tapd')) {
                $element = Find-ById $root ('Nav_' + $key)
                if (($case.Menus -contains $key) -ne ($null -ne $element)) { throw "Wrong menu $key for role $($case.Role)" }
            }
            if ($case.Role -eq 'Admin') { Save-Window $process 'admin-home' }
            $menu = Find-ById $root ('Nav_' + $case.Menus[0])
            $menu.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
            $list = Wait-Element $root 'DocumentsList'
            $first = $list.FindFirst([System.Windows.Automation.TreeScope]::Children,
                [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty,
                    [System.Windows.Automation.ControlType]::ListItem))
            if ($null -eq $first) { throw "No synthetic document rendered for $($case.Role)" }
            $first.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
            [void](Wait-Element $root 'DetailTitle')
            Save-Window $process ($case.Role.Replace('|', '-') + '-documents')
            Write-Host "PASS: $($case.Role) menu, document navigation, and detail selection."
        }
        finally {
            if ($null -ne $process -and !$process.HasExited) { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue }
        }
    }
}
finally {
    $env:ETIKA_UI_PREVIEW = $oldPreview
    $log = Join-Path $env:LOCALAPPDATA 'ETIKA/Desktop/Logs/startup.log'
    if (Test-Path $log) { Copy-Item $log (Join-Path $diagnostics.FullName 'startup.log') }
}
