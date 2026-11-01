using Etika.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;
using Windows.System;

namespace Etika.Desktop;

public sealed partial class MainWindow : Window
{
    private EtikaApiClient? _api;
    private string? _challengeKey;
    private string? _challengeServer;
    private WorkspaceProfile _profile = WorkspaceProfile.ForLevel(null);
    private WorkspaceMenu? _documentMenu;
    private string _view = "home";
    private bool _busy, _loaded, _detailSelected;
    private bool _preview = false;
    private int _page = 1, _lastPage = 1;
    private readonly CancellationTokenSource _lifetime = new();
    private CancellationTokenSource? _operation;
    private Func<Task>? _retryOperation;
    private Identity? _signedInUser;
    private bool _preferencesReady;
    private CancellationToken RequestToken => _operation?.Token ?? _lifetime.Token;
    private readonly HashSet<PdfWindow> _pdfWindows = [];

    public MainWindow()
    {
        InitializeComponent();
        SystemBackdrop = new MicaBackdrop();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1440, 960));
        Closed += (_, _) => { _lifetime.Cancel(); ClosePdfs(); _api?.Dispose(); };
    }

    private async void Root_Loaded(object sender, RoutedEventArgs e)
    {
        if (_loaded) return;
        _loaded = true;
#if DEBUG
        var previewRole = Environment.GetEnvironmentVariable("ETIKA_UI_PREVIEW");
        if (!string.IsNullOrWhiteSpace(previewRole))
        {
            _preview = true;
            PreviewStrip.Visibility = Visibility.Visible;
            PreviewRoleBox.ItemsSource = new[] { "Admin", "Eksekutif", "SKPD", "Berkas", "TAPD", "SKPD|TAPD" };
            await RunAsync(async () =>
            {
                PreviewRoleBox.SelectedItem = previewRole;
                await EnterWorkspaceAsync(PreviewData.User(previewRole));
            });
            return;
        }
#endif
        var preferences = SessionStore.Preferences;
        ThemeBox.SelectedIndex = preferences.Theme;
        _preferencesReady = true;
        ServerBox.Text = preferences.Server;
#if DEBUG
        if (Environment.GetEnvironmentVariable("ETIKA_DEV_SERVER") is { Length: > 0 } developmentServer
            && Uri.TryCreate(developmentServer, UriKind.Absolute, out var developmentUri) && developmentUri.IsLoopback)
            ServerBox.Text = developmentServer;
#endif
        await RunAsync(async () =>
        {
            SetClient();
            var saved = SessionStore.Load(_api!.Server);
            if (saved is not null)
            {
                RememberBox.IsChecked = true;
                _api.SetToken(saved.Token);
                await EnterWorkspaceAsync(await _api.MeAsync(RequestToken));
            }
            else await ChallengeAsync();
        });
    }

    private void SetClient()
    {
        _api?.Dispose();
        _api = new EtikaApiClient(ServerBox.Text);
        if (!SessionStore.SaveServer(_api.Server))
        {
            ErrorBar.Severity = InfoBarSeverity.Warning;
            ErrorBar.Message = "Alamat server digunakan, tetapi belum dapat disimpan pada perangkat ini.";
            ErrorBar.IsOpen = true;
        }
    }

    private async Task ChallengeAsync()
    {
        _challengeKey = _challengeServer = null;
        LoginButton.IsEnabled = false;
        CaptchaImage.Source = null;
        CaptchaBox.Text = "";
        var challenge = await _api!.ChallengeAsync(RequestToken);
        if (string.IsNullOrWhiteSpace(challenge.Key) || string.IsNullOrEmpty(challenge.Img)
            || !challenge.Img.StartsWith("data:image/", StringComparison.Ordinal) || challenge.Img.Length > 500_000)
            throw new ApiException(502, "Gambar kode verifikasi tidak valid.");
        var comma = challenge.Img.IndexOf(',');
        if (comma < 0) throw new ApiException(502, "Kode verifikasi tidak valid.");
        CaptchaImage.Source = await BitmapAsync(Convert.FromBase64String(challenge.Img[(comma + 1)..]));
        _challengeKey = challenge.Key;
        _challengeServer = _api.Server;
        LoginButton.IsEnabled = true;
    }

    private async void Challenge_Click(object sender, RoutedEventArgs e) => await RunAsync(async () =>
    {
        SetClient();
        await ChallengeAsync();
    });

    private async void Login_Click(object sender, RoutedEventArgs e) => await RunAsync(async () =>
    {
        using var candidate = new EtikaApiClient(ServerBox.Text);
        if (_challengeKey is null || candidate.Server != _challengeServer)
        {
            SetClient();
            await ChallengeAsync();
            throw new ApiException(422, "Isi kode verifikasi yang baru ditampilkan.");
        }
        LoginResult result;
        try
        {
            result = await _api!.LoginAsync(EmailBox.Text.Trim(), PasswordBox.Password,
                _challengeKey, CaptchaBox.Text.Trim(), Environment.MachineName, RequestToken);
        }
        catch
        {
            _challengeKey = null;
            try { await ChallengeAsync(); } catch { /* retain the original login failure */ }
            throw;
        }
        PasswordBox.Password = "";
        SessionStore.Clear(_api!.Server);
        bool rememberFailed = RememberBox.IsChecked == true && !SessionStore.Save(_api.Server, new SavedSession(result.Token, result.ExpiresAt));
        if (rememberFailed) RememberBox.IsChecked = false;
        await EnterWorkspaceAsync(result.User);
        if (rememberFailed)
        {
            ErrorBar.Severity = InfoBarSeverity.Warning;
            ErrorBar.Message = "Anda sudah masuk, tetapi Windows belum dapat menyimpan sesi. Masuk kembali setelah aplikasi ditutup.";
            ErrorBar.IsOpen = true;
        }
    }, ReloadChallengeAsync);

    private async Task EnterWorkspaceAsync(Identity user)
    {
        _signedInUser = user;
        _profile = WorkspaceProfile.ForLevel(user.Level);
        if (_profile.Documents.Count == 0) throw new ApiException(403, "Peran akun belum didukung.");
        ServerContext context;
#if DEBUG
        if (_preview) context = PreviewData.Context;
        else
#endif
            context = await _api!.ContextAsync(RequestToken);
        UserName.Text = AccountName.Text = user.Name;
        UserRole.Text = AccountRole.Text = HomeRole.Text = _profile.RoleLabel;
        AccountEmail.Text = user.Email;
        ScopeText.Text = AccountScope.Text = _profile.ScopeDescription;
        WelcomeTitle.Text = $"Selamat datang, {user.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? user.Name}.";
        HeaderAvatar.DisplayName = SidebarAvatar.DisplayName = AccountAvatar.DisplayName = user.Name;
        ConnectedServer.Text = _preview ? "Pratinjau lokal · Tanpa koneksi server" : _api!.Server;
        Workspace.MenuItems.Clear();
        Workspace.MenuItems.Add(NavItem("Beranda", "\uE80F", "home"));
        Workspace.MenuItems.Add(new NavigationViewItemHeader { Content = "RUANG KERJA" });
        foreach (var menu in _profile.Documents)
        {
            var item = NavItem(menu.Title, menu.Glyph, menu);
            AutomationProperties.SetAutomationId(item, "Nav_" + menu.Key);
            Workspace.MenuItems.Add(item);
        }
        Workspace.FooterMenuItems.Clear();
        Workspace.FooterMenuItems.Add(NavItem("Akun saya", "\uE77B", "account"));
        Workspace.FooterMenuItems.Add(NavItem("Pengaturan", "\uE713", "settings"));
        var logout = NavItem("Keluar", "\uE89B", "logout");
        Workspace.FooterMenuItems.Add(logout);
        foreach (var item in Workspace.FooterMenuItems.OfType<NavigationViewItem>())
            AutomationProperties.SetAutomationId(item, "Nav_" + item.Tag);
        HomeShortcuts.ItemsSource = _profile.Documents;
        _documentMenu = _profile.Documents[0];
        HomeCountCaption.Text = _documentMenu.Title;
        YearBox.ItemsSource = context.Tahun;
        YearBox.SelectedItem = context.Tahun.FirstOrDefault(x => x.Tahun == context.TahunAktif) ?? context.Tahun.FirstOrDefault();
        LoginPanel.Visibility = Visibility.Collapsed;
        Workspace.Visibility = HeaderSearch.Visibility = ProfileButton.Visibility = Visibility.Visible;
        Workspace.SelectedItem = Workspace.MenuItems[0];
        ShowView("home");
        SearchBox.Text = "";
        _page = 1;
        await LoadDocumentsAsync();
    }

    private static NavigationViewItem NavItem(string title, string glyph, object tag) =>
        new() { Content = title, Icon = new FontIcon { Glyph = glyph }, Tag = tag };

    private void ShowView(string view)
    {
        _view = view;
        HomePanel.Visibility = view == "home" ? Visibility.Visible : Visibility.Collapsed;
        DocumentsPanel.Visibility = view == "documents" ? Visibility.Visible : Visibility.Collapsed;
        AccountPanel.Visibility = view == "account" ? Visibility.Visible : Visibility.Collapsed;
        SettingsPanel.Visibility = view == "settings" ? Visibility.Visible : Visibility.Collapsed;
        PageTitle.Text = view switch { "documents" => _documentMenu!.Title, "account" => "Akun saya", "settings" => "Pengaturan", _ => "Beranda" };
        PageDescription.Text = view switch
        {
            "documents" => _documentMenu!.Description,
            "account" => "Identitas dan kewenangan akun ETIKA Anda.",
            "settings" => "Sesuaikan tampilan ruang kerja Anda.",
            _ => "Dokumen dan penugasan Anda dalam satu ruang kerja."
        };
        FilterCaption.Text = _documentMenu?.Status is null ? "Seluruh status" : $"Tahap: {_documentMenu.Title}";
        UpdateDocumentLayout();
    }

    private async void Navigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (_busy) return;
        if (args.IsSettingsSelected) { ShowView("settings"); return; }
        switch ((args.SelectedItem as NavigationViewItem)?.Tag)
        {
            case WorkspaceMenu menu: await RunAsync(() => OpenDocumentMenuAsync(menu)); break;
            case "home":
                await RunAsync(async () =>
                {
                    _documentMenu = _profile.Documents[0];
                    SearchBox.Text = "";
                    _page = 1;
                    ShowView("home");
                    await LoadDocumentsAsync();
                });
                break;
            case "account": ShowView("account"); break;
            case "settings": ShowView("settings"); break;
            case "logout": Logout_Click(sender, new RoutedEventArgs()); break;
        }
    }

    private async Task OpenDocumentMenuAsync(WorkspaceMenu menu, bool keepSearch = false)
    {
        _documentMenu = menu;
        if (!keepSearch) SearchBox.Text = "";
        _page = 1;
        Workspace.SelectedItem = Workspace.MenuItems.OfType<NavigationViewItem>().First(x => ReferenceEquals(x.Tag, menu));
        ShowView("documents");
        await LoadDocumentsAsync();
    }
    private async void OpenDocuments_Click(object sender, RoutedEventArgs e) =>
        await RunAsync(() => OpenDocumentMenuAsync(_profile.Documents[0]));
    private async void Shortcut_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string key } && _profile.Documents.FirstOrDefault(x => x.Key == key) is { } menu)
            await RunAsync(() => OpenDocumentMenuAsync(menu));
    }
    private void Account_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        Workspace.SelectedItem = Workspace.FooterMenuItems[0];
        ShowView("account");
    }

    private async Task LoadDocumentsAsync()
    {
        ClearDetail();
        DocumentsList.ItemsSource = null;
        HomeCount.Text = "—";
        DocumentsCount.Text = PageLabel.Text = "";
        EmptyState.Visibility = Visibility.Visible;
        EmptyStateText.Text = "Memuat dokumen…";
        PreviousButton.IsEnabled = NextButton.IsEnabled = false;
        if (YearBox.SelectedItem is not BudgetYear year)
        {
            EmptyState.Visibility = Visibility.Visible;
            EmptyStateText.Text = "Belum ada tahun anggaran tersedia.";
            PageLabel.Text = "";
            PreviousButton.IsEnabled = NextButton.IsEnabled = false;
            return;
        }
        PageResult<DocumentSummary> result;
#if DEBUG
        if (_preview) result = PreviewData.Documents(_profile, year.Tahun, _page, SearchBox.Text.Trim(), _documentMenu?.Status);
        else
#endif
            result = await _api!.DocumentsAsync(year.Tahun, _page, SearchBox.Text.Trim(), _documentMenu?.Status, RequestToken);
        if (_page > Math.Max(1, result.Meta.LastPage))
        {
            _page = Math.Max(1, result.Meta.LastPage);
            await LoadDocumentsAsync();
            return;
        }
        DocumentsList.ItemsSource = result.Data;
        EmptyState.Visibility = result.Data.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyStateText.Text = string.IsNullOrWhiteSpace(SearchBox.Text) ? "Belum ada dokumen untuk pilihan ini." : "Tidak ada dokumen yang cocok dengan pencarian.";
        _lastPage = result.Meta.LastPage;
        DocumentsCount.Text = $"{result.Meta.Total:N0} dokumen";
        if (_view == "home") HomeCount.Text = result.Meta.Total.ToString("N0");
        PageLabel.Text = $"Halaman {_page} dari {_lastPage}";
        PreviousButton.IsEnabled = _page > 1;
        NextButton.IsEnabled = _page < _lastPage;
    }

    private void ClearDetail()
    {
        DetailScroll.ChangeView(null, 0, null, disableAnimation: true);
        _detailSelected = false;
        DetailTitle.Text = DetailSubtitle.Text = DetailStatus.Text = DetailBudget.Text = DetailNotes.Text = "";
        AttachmentsList.ItemsSource = null;
        DetailEmpty.Visibility = Visibility.Visible;
        DetailScroll.Visibility = Visibility.Collapsed;
        UpdateDocumentLayout();
    }

    private async void Document_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_busy || DocumentsList.SelectedItem is not DocumentSummary selected) return;
        await RunAsync(async () =>
        {
            ClearDetail();
            DocumentDetail doc;
#if DEBUG
            if (_preview) doc = PreviewData.Detail(selected);
            else
#endif
                doc = await _api!.DocumentAsync(selected.Id, RequestToken);
            DetailTitle.Text = doc.DisplayTitle;
            DetailSubtitle.Text = doc.DisplaySubtitle;
            DetailStatus.Text = doc.StatusLabel;
            DetailBudget.Text = doc.DisplayPagu;
            DetailNotes.Text = doc.Keterangan ?? "";
            DetailNotes.Visibility = string.IsNullOrWhiteSpace(doc.Keterangan) ? Visibility.Collapsed : Visibility.Visible;
            AttachmentsList.ItemsSource = doc.Lampiran;
            AttachmentHeading.Text = doc.Lampiran.Count == 0 ? "Belum ada lampiran PDF" : $"Lampiran PDF ({doc.Lampiran.Count})";
            _detailSelected = true;
            DetailEmpty.Visibility = Visibility.Collapsed;
            DetailScroll.Visibility = Visibility.Visible;
            UpdateDocumentLayout();
            DetailScroll.ChangeView(null, 0, null, disableAnimation: true);
        });
    }

    private void DocumentsLayout_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateDocumentLayout();
    private void UpdateDocumentLayout()
    {
        if (DocumentsLayout is null || DetailsColumn is null) return;
        bool wide = DocumentsLayout.ActualWidth >= 960;
        DetailsColumn.Width = new GridLength(wide ? 380 : 0);
        Grid.SetColumn(DetailCard, wide ? 1 : 0);
        ListCard.Visibility = wide || !_detailSelected ? Visibility.Visible : Visibility.Collapsed;
        DetailCard.Visibility = wide || _detailSelected ? Visibility.Visible : Visibility.Collapsed;
        DetailBackButton.Visibility = wide ? Visibility.Collapsed : Visibility.Visible;
    }
    private void DetailBack_Click(object sender, RoutedEventArgs e)
    {
        DocumentsList.SelectedItem = null;
        ClearDetail();
    }
    private void Root_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (LoginIntroColumn is null) return;
        bool wide = e.NewSize.Width >= 1000;
        LoginIntroColumn.Width = new GridLength(wide ? 1 : 0, wide ? GridUnitType.Star : GridUnitType.Pixel);
        LoginIntro.Visibility = wide ? Visibility.Visible : Visibility.Collapsed;
        LoginLayout.ColumnSpacing = wide ? 64 : 0;
    }
    private void Theme_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (Root is null) return;
        Root.RequestedTheme = ThemeBox.SelectedIndex switch { 1 => ElementTheme.Light, 2 => ElementTheme.Dark, _ => ElementTheme.Default };
        if (_preferencesReady && !_preview && !SessionStore.SaveTheme(ThemeBox.SelectedIndex))
        {
            ErrorBar.Severity = InfoBarSeverity.Warning;
            ErrorBar.Message = "Tema diterapkan, tetapi pengaturannya belum dapat disimpan pada perangkat ini.";
            ErrorBar.IsOpen = true;
        }
    }

    private async void Attachment_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: Attachment attachment }) return;
        if (_preview)
        {
            ErrorBar.Severity = InfoBarSeverity.Informational;
            ErrorBar.Message = "Lampiran ini adalah data contoh. Gunakan profil ETIKA Desktop (Unpackaged) untuk membuka PDF dari server.";
            ErrorBar.IsOpen = true;
            return;
        }
        await RunAsync(async () =>
        {
            var bytes = await _api!.PdfAsync(attachment.Id, RequestToken);
            var viewer = new PdfWindow(attachment.Nama, Root.ActualTheme);
            _pdfWindows.Add(viewer);
            viewer.Closed += (_, _) => _pdfWindows.Remove(viewer);
            viewer.Activate();
            await viewer.LoadAsync(bytes, RequestToken);
        });
    }

    private async void Year_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_busy || (!_preview && _api?.HasToken != true)) return;
        _page = 1;
        await RunAsync(LoadDocumentsAsync);
    }
    private async Task SearchAsync()
    {
        if (_documentMenu is not null) await OpenDocumentMenuAsync(_documentMenu, keepSearch: true);
    }
    private async void Search_Click(object sender, RoutedEventArgs e) => await RunAsync(SearchAsync);
    private async void Search_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && !_busy) { e.Handled = true; await RunAsync(SearchAsync); }
    }
    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        if (_view is not ("home" or "documents")) return;
        await RunAsync(async () =>
        {
            ServerContext context;
#if DEBUG
            if (_preview) context = PreviewData.Context;
            else
#endif
                context = await _api!.ContextAsync(RequestToken);
            var selectedYear = (YearBox.SelectedItem as BudgetYear)?.Tahun;
            YearBox.ItemsSource = context.Tahun;
            YearBox.SelectedItem = context.Tahun.FirstOrDefault(x => x.Tahun == selectedYear)
                ?? context.Tahun.FirstOrDefault(x => x.Tahun == context.TahunAktif) ?? context.Tahun.FirstOrDefault();
            await LoadDocumentsAsync();
        });
    }
    private async void Previous_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _page <= 1) return;
        _page--;
        await RunAsync(LoadDocumentsAsync);
    }
    private async void Next_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _page >= _lastPage) return;
        _page++;
        await RunAsync(LoadDocumentsAsync);
    }
    private async void PreviewRole_Changed(object sender, SelectionChangedEventArgs e)
    {
#if DEBUG
        if (!_preview || _busy || PreviewRoleBox.SelectedItem is not string role) return;
        await RunAsync(() => EnterWorkspaceAsync(PreviewData.User(role)));
#endif
    }
    private async void Logout_Click(object sender, RoutedEventArgs e)
    {
        if (_preview)
        {
            ErrorBar.Severity = InfoBarSeverity.Informational;
            ErrorBar.Message = "Mode pratinjau memakai akun contoh dan tidak menyimpan sesi login.";
            ErrorBar.IsOpen = true;
            return;
        }
        await RunAsync(async () =>
        {
            string? warning = null;
            try { await _api!.LogoutAsync(RequestToken); }
            catch { warning = "Sesi di perangkat sudah dihapus. Pencabutan sesi di server belum berhasil."; }
            ResetSession();
            if (warning is not null) { ErrorBar.Message = warning; ErrorBar.IsOpen = true; }
            await ChallengeAsync();
        }, ReloadChallengeAsync);
    }

    private void ClosePdfs()
    {
        foreach (var window in _pdfWindows.ToArray()) window.Close();
        _pdfWindows.Clear();
    }
    private void ResetSession()
    {
        if (_api is not null) SessionStore.Clear(_api.Server);
        _api?.ClearToken();
        ClosePdfs();
        ClearDetail();
        DocumentsList.ItemsSource = YearBox.ItemsSource = HomeShortcuts.ItemsSource = null;
        Workspace.MenuItems.Clear();
        Workspace.FooterMenuItems.Clear();
        SearchBox.Text = UserName.Text = UserRole.Text = AccountName.Text = AccountEmail.Text = AccountRole.Text = AccountScope.Text = "";
        HeaderAvatar.DisplayName = SidebarAvatar.DisplayName = AccountAvatar.DisplayName = "";
        WelcomeTitle.Text = HomeRole.Text = HomeCount.Text = ConnectedServer.Text = "";
        _profile = WorkspaceProfile.ForLevel(null);
        _documentMenu = null;
        _signedInUser = null;
        Workspace.Visibility = HeaderSearch.Visibility = ProfileButton.Visibility = Visibility.Collapsed;
        LoginPanel.Visibility = Visibility.Visible;
        _challengeKey = _challengeServer = null;
        LoginButton.IsEnabled = false;
        CaptchaImage.Source = null;
        PasswordBox.Password = CaptchaBox.Text = "";
        DocumentsCount.Text = PageLabel.Text = "";
    }

    private async Task ReloadChallengeAsync()
    {
        SetClient();
        await ChallengeAsync();
    }
    private async void Retry_Click(object sender, RoutedEventArgs e)
    {
        if (_retryOperation is { } retry) await RunAsync(retry);
    }
    private void Cancel_Click(object sender, RoutedEventArgs e) => _operation?.Cancel();
    private void Server_Changed(object sender, TextChangedEventArgs e)
    {
        if (LoginButton is null) return;
        _challengeKey = _challengeServer = null;
        LoginButton.IsEnabled = false;
        CaptchaImage.Source = null;
        CaptchaBox.Text = "";
    }
    private void Login_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && !_busy && LoginButton.IsEnabled)
        {
            e.Handled = true;
            Login_Click(sender, e);
        }
    }
    private async Task RunAsync(Func<Task> operation, Func<Task>? retry = null)
    {
        if (_busy || _lifetime.IsCancellationRequested) return;
        _busy = true;
        _retryOperation = null;
        ErrorRetryButton.Visibility = Visibility.Collapsed;
        ErrorBar.IsOpen = false;
        ErrorBar.Severity = InfoBarSeverity.Error;
        _operation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        LoginPanel.IsEnabled = Workspace.IsEnabled = SearchBox.IsEnabled = SearchSubmitButton.IsEnabled = ProfileButton.IsEnabled = PreviewRoleBox.IsEnabled = false;
        BusyBar.Visibility = Visibility.Visible;
        try { await operation(); }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception ex)
        {
            bool cancelled = ex is OperationCanceledException && _operation.IsCancellationRequested;
            bool retryable = ex is HttpRequestException or OperationCanceledException || ex is ApiException { StatusCode: >= 500 };
            if (retryable) _retryOperation = retry ?? operation;
            if (retryable && _api?.HasToken == true && LoginPanel.Visibility == Visibility.Visible && _signedInUser is { } user)
                _retryOperation = () => EnterWorkspaceAsync(user);
            if (ex is ApiException { StatusCode: 401 or 403 })
            {
                ResetSession();
                _retryOperation = ReloadChallengeAsync;
                try { await ChallengeAsync(); _retryOperation = null; } catch { /* sign-in remains available for a manual retry */ }
            }
            if (DocumentsList.ItemsSource is null) EmptyStateText.Text = "Dokumen belum berhasil dimuat. Silakan coba lagi.";
            ErrorBar.Severity = cancelled ? InfoBarSeverity.Informational : InfoBarSeverity.Error;
            ErrorBar.Message = ex switch
            {
                HttpRequestException => "Server belum dapat dihubungi. Periksa koneksi dan alamat server, lalu coba lagi.",
                OperationCanceledException when cancelled => "Permintaan dibatalkan. Anda dapat mencoba kembali.",
                OperationCanceledException => "Server terlalu lama merespons. Silakan coba kembali.",
                ApiException or ArgumentException => ex.Message,
                _ => "Operasi belum berhasil. Silakan coba kembali."
            };
            ErrorRetryButton.Visibility = _retryOperation is null ? Visibility.Collapsed : Visibility.Visible;
            ErrorBar.IsOpen = true;
        }
        finally
        {
            _operation.Dispose();
            _operation = null;
            _busy = false;
            if (!_lifetime.IsCancellationRequested)
            {
                LoginPanel.IsEnabled = Workspace.IsEnabled = SearchBox.IsEnabled = SearchSubmitButton.IsEnabled = ProfileButton.IsEnabled = PreviewRoleBox.IsEnabled = true;
                BusyBar.Visibility = Visibility.Collapsed;
            }
        }
    }

    internal static async Task<BitmapImage> BitmapAsync(byte[] bytes)
    {
        using var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream.GetOutputStreamAt(0))) { writer.WriteBytes(bytes); await writer.StoreAsync(); }
        stream.Seek(0);
        var image = new BitmapImage();
        await image.SetSourceAsync(stream);
        return image;
    }
}
