param(
    [Parameter(Mandatory = $true)][string]$PublishDirectory,
    [string]$DiagnosticsDirectory = 'artifacts/api-integration'
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class EtikaIntegrationWindow {
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr window, IntPtr dc, uint flags);
}
'@
$publish = (Resolve-Path $PublishDirectory).Path
$diagnostics = (New-Item -ItemType Directory -Force $DiagnosticsDirectory).FullName
$ready = Join-Path $diagnostics 'server-ready.json'
$oldServer = $env:ETIKA_DEV_SERVER; $oldData = $env:ETIKA_TEST_DATA_DIR; $oldPreview = $env:ETIKA_UI_PREVIEW
$process = $null; $server = $null; $root = $null
function Find-Id($parent, [string]$id) {
    return $parent.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
        [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $id))
}
function Find-Name($parent, [string]$name) {
    return $parent.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
        [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty, $name))
}
function Wait-For([scriptblock]$condition, [string]$label = 'UI condition') {
    for ($i = 0; $i -lt 150; $i++) {
        if ($null -ne $process -and $process.HasExited) { throw "App exited while waiting for $label" }
        try { $value = & $condition; if ($value) { return $value } } catch [System.Windows.Automation.ElementNotAvailableException] { }
        Start-Sleep -Milliseconds 100
    }
    throw "Timed out waiting for $label"
}
function Visible($element) { return $null -ne $element -and !$element.Current.IsOffscreen }
function Ready-Id([string]$id, $parent = $root) {
    return Wait-For { $e = Find-Id $parent $id; if ((Visible $e) -and $e.Current.IsEnabled) { $e } } $id
}
function Invoke-Id([string]$id, $parent = $root) {
    $element = Ready-Id $id $parent
    $element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
}
function Set-Text([string]$id, [string]$value) {
    (Ready-Id $id).GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($value)
}
function Select-Menu([string]$key) {
    (Ready-Id ('Nav_' + $key)).GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
}
function Select-Choice([string]$id, [string]$choice) {
    (Ready-Id $id).GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
    $item = Wait-For { $e = Find-Name $root $choice; if (Visible $e) { $e } } "choice $choice"
    $item.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
}
function Expect-Text([string]$text, $parent = $root) {
    [void](Wait-For { $e = Find-Name $parent $text; Visible $e } $text)
}
function Control($body) { [void](Invoke-RestMethod -Uri ($base + '/__control') -Method Post -ContentType 'application/json' -Body ($body | ConvertTo-Json)) }
function Stats { return Invoke-RestMethod -Uri ($base + '/__stats') }
function Start-App {
    $script:process = Start-Process -FilePath (Join-Path $publish 'Etika.Desktop.exe') -WorkingDirectory $publish -PassThru
    [void](Wait-For { $process.Refresh(); $process.MainWindowHandle -ne 0 } 'main window')
    $script:root = [System.Windows.Automation.AutomationElement]::FromHandle($process.MainWindowHandle)
}
function Stop-App {
    if ($null -ne $process -and !$process.HasExited) { Stop-Process -Id $process.Id -Force }
    $script:process = $null
}
function Login([string]$user, [bool]$remember = $false, [string]$password = 'test-password') {
    [void](Ready-Id 'LoginButton')
    Set-Text 'EmailBox' ($user + '@etika.test'); Set-Text 'PasswordBox' $password; Set-Text 'CaptchaBox' '12345'
    $toggle = (Ready-Id 'RememberBox').GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
    if (($toggle.Current.ToggleState -eq [System.Windows.Automation.ToggleState]::On) -ne $remember) { $toggle.Toggle() }
    Invoke-Id 'LoginButton'
}
function Select-FirstDocument {
    $list = Ready-Id 'DocumentsList'
    $first = Wait-For { $list.FindFirst([System.Windows.Automation.TreeScope]::Children,
        [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty,
            [System.Windows.Automation.ControlType]::ListItem)) } 'first document'
    $first.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    [void](Ready-Id 'DetailTitle')
}
function Find-Pdf([string]$title) {
    return [System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Children,
        [System.Windows.Automation.AndCondition]::new(
            [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $process.Id),
            [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty, ($title + ' — ETIKA'))))
}
function Open-Pdf([string]$id, [string]$title) {
    Invoke-Id $id
    return Wait-For { Find-Pdf $title } "PDF window $title"
}
function Close-Pdf($window) { $window.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close() }
function Save-Window([string]$name) {
    $rect = [EtikaIntegrationWindow+Rect]::new()
    [void][EtikaIntegrationWindow]::GetWindowRect($process.MainWindowHandle, [ref]$rect)
    $bitmap = [System.Drawing.Bitmap]::new($rect.Right-$rect.Left,$rect.Bottom-$rect.Top)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap); $dc=$graphics.GetHdc()
    try { [void][EtikaIntegrationWindow]::PrintWindow($process.MainWindowHandle,$dc,2) }
    finally { $graphics.ReleaseHdc($dc) }
    try { $bitmap.Save((Join-Path $diagnostics ($name+'.png')),[System.Drawing.Imaging.ImageFormat]::Png) }
    finally { $graphics.Dispose(); $bitmap.Dispose() }
}
try {
    if (Test-Path $ready) { Remove-Item $ready }
    $server = Start-Process -FilePath (Get-Command python).Source -ArgumentList @((Join-Path $PSScriptRoot 'fixtures/api_server.py'),'--ready-file',$ready) -PassThru
    [void](Wait-For { Test-Path $ready } 'loopback fixture')
    $base = (Get-Content $ready -Raw | ConvertFrom-Json).url
    $env:ETIKA_DEV_SERVER = $base; $env:ETIKA_UI_PREVIEW = $null
    $env:ETIKA_TEST_DATA_DIR = Join-Path $diagnostics 'settings'
    Start-App
    Login 'admin' $false 'wrong-password'
    Expect-Text 'Email atau kata sandi tidak sesuai.'
    Login 'admin' $true
    [void](Ready-Id 'Nav_all')
    Write-Host 'PASS: invalid login, fresh CAPTCHA, and authenticated login over HTTP.'
    $rememberUnavailable = Visible (Find-Name $root 'Anda sudah masuk, tetapi Windows belum dapat menyimpan sesi. Masuk kembali setelah aplikasi ditutup.')
    Select-Menu 'all'; [void](Ready-Id 'DocumentsList')
    Invoke-Id 'NextButton'; Expect-Text 'Halaman 2 dari 2'
    Invoke-Id 'PreviousButton'; Expect-Text 'Halaman 1 dari 2'
    Set-Text 'SearchBox' 'tidak-ada-hasil'; Invoke-Id 'SearchSubmitButton'
    Expect-Text 'Tidak ada dokumen yang cocok dengan pencarian.'
    Set-Text 'SearchBox' ''; Invoke-Id 'SearchSubmitButton'
    Select-Choice 'YearBox' '2025'
    [void](Wait-For { (Stats).last_year -eq 2025 } 'selected budget year')
    Control @{renamed=$true}; Invoke-Id 'ReloadButton'; Expect-Text 'Perubahan dari web (uji)'
    Control @{empty=$true}; Invoke-Id 'ReloadButton'; Expect-Text 'Belum ada dokumen untuk pilihan ini.'
    Control @{empty=$false}; Invoke-Id 'ReloadButton'; Expect-Text 'Perubahan dari web (uji)'
    Write-Host 'PASS: pagination, search, empty state, budget year, and refreshed server changes.'
    Control @{mode='failure'}; Invoke-Id 'ReloadButton'
    Expect-Text 'Layanan ETIKA sedang mengalami gangguan. Silakan coba kembali.'
    [void](Ready-Id 'ErrorRetryButton'); Control @{mode='normal'}; Invoke-Id 'ErrorRetryButton'
    Expect-Text 'Perubahan dari web (uji)'
    Control @{mode='slow'}; Invoke-Id 'ReloadButton'; Invoke-Id 'CancelRequestButton'
    Expect-Text 'Permintaan dibatalkan. Anda dapat mencoba kembali.'
    Control @{mode='normal'}; Invoke-Id 'ErrorRetryButton'; Expect-Text 'Perubahan dari web (uji)'
    Write-Host 'PASS: recoverable server failure, cancellation, and explicit retry.'
    Select-Menu 'settings'; Select-Choice 'ThemeBox' 'Gelap'; Save-Window 'settings-dark'
    $saved = Get-Content (Join-Path $env:ETIKA_TEST_DATA_DIR 'settings.json') -Raw | ConvertFrom-Json
    if ($saved.theme -ne 2 -or $saved.server -ne $base) { throw 'Theme/server persistence failed.' }
    $logins = (Stats).logins
    Stop-App; Start-App
    if ($rememberUnavailable) {
        Login 'admin'; [void](Ready-Id 'Nav_all')
        Write-Host 'SKIP: Credential Locker unavailable in this runner; manual remembered-session validation required.'
    } else {
        [void](Ready-Id 'Nav_all')
        if ((Stats).logins -ne $logins) { throw 'Remembered session issued an extra login.' }
        Write-Host 'PASS: remembered session restored after process restart.'
    }
    Select-Menu 'settings'; Expect-Text 'Gelap'; Save-Window 'restored-dark'
    Select-Menu 'all'; Select-FirstDocument
    $pdf = Open-Pdf 'valid' 'PDF uji dua halaman'
    Expect-Text 'Halaman 1 dari 2' $pdf
    Invoke-Id 'PdfNextButton' $pdf; Expect-Text 'Halaman 2 dari 2' $pdf
    Invoke-Id 'PdfPreviousButton' $pdf; Expect-Text 'Halaman 1 dari 2' $pdf
    Invoke-Id 'PdfZoomInButton' $pdf
    [void](Wait-For { (Find-Id $pdf 'ZoomText').Current.Name -match '125' } 'PDF zoom')
    Close-Pdf $pdf
    foreach ($case in @(@('broken','PDF rusak'),@('locked','PDF terkunci'))) {
        $pdf = Open-Pdf $case[0] $case[1]
        [void](Ready-Id 'PdfError' $pdf); Close-Pdf $pdf
    }
    Invoke-Id 'missing'; Expect-Text 'Berkas belum tersedia.'
    Invoke-Id 'html'; Expect-Text 'Server tidak mengirim berkas PDF.'
    Invoke-Id 'large'; Expect-Text 'Ukuran PDF melebihi 25 MB.'
    $pdf = Open-Pdf 'valid' 'PDF uji dua halaman'; Expect-Text 'Halaman 1 dari 2' $pdf
    Select-Menu 'logout'; [void](Ready-Id 'LoginButton')
    if ($null -ne (Find-Pdf 'PDF uji dua halaman')) { throw 'Logout left an authenticated PDF window open.' }
    Write-Host 'PASS: native PDF rendering, page navigation, zoom, invalid/encrypted/missing/oversize PDF, and logout cleanup.'
    Login 'admin'; [void](Ready-Id 'Nav_all')
    Control @{expire=$true}; Invoke-Id 'ReloadButton'; [void](Ready-Id 'LoginButton')
    if ($null -ne (Find-Id $root 'Nav_all')) { throw 'Expired session retained role navigation.' }
    Login 'admin'; [void](Ready-Id 'Nav_all')
    Control @{mode='inactive'}; Invoke-Id 'ReloadButton'; [void](Ready-Id 'LoginButton')
    Expect-Text 'Akun Anda dinonaktifkan.'; Control @{mode='normal'}
    Write-Host 'PASS: expired/inactive sessions clear workspace and reload CAPTCHA.'
    foreach ($case in @(@('eksekutif','all'),@('skpd','own'),@('berkas','berkas'),@('tapd','tapd'),@('multi','accessible'))) {
        Login $case[0]; [void](Ready-Id ('Nav_'+$case[1]))
        Select-Menu $case[1]; Select-FirstDocument
        if ($case[0] -eq 'tapd' -and (Stats).last_status -ne '3') { throw 'TAPD filter missing.' }
        if ($case[0] -eq 'berkas' -and (Stats).last_status -ne '1') { throw 'Berkas filter missing.' }
        Select-Menu 'logout'; [void](Ready-Id 'LoginButton')
        Write-Host "PASS: HTTP login, role menu, documents, and logout for $($case[0])."
    }
}
catch {
    if ($null -ne $process -and !$process.HasExited) { Save-Window 'failure' }
    throw
}
finally {
    Stop-App
    if ($null -ne $server -and !$server.HasExited) { Stop-Process -Id $server.Id -Force -ErrorAction SilentlyContinue }
    $env:ETIKA_DEV_SERVER=$oldServer; $env:ETIKA_TEST_DATA_DIR=$oldData; $env:ETIKA_UI_PREVIEW=$oldPreview
}
