using Etika.Core;
using Microsoft.UI.Xaml;
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
    private bool _busy;
    private int _page = 1;
    private int _lastPage = 1;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly HashSet<PdfWindow> _pdfWindows = [];

    public MainWindow()
    {
        InitializeComponent();
        SystemBackdrop = new MicaBackdrop();
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1360, 900));
        Closed += (_, _) =>
        {
            _lifetime.Cancel();
            ClosePdfs();
            _api?.Dispose();
        };
    }

    private async void Root_Loaded(object sender, RoutedEventArgs e)
    {
        ServerBox.Text = SessionStore.LoadServer();
        await RunAsync(async () =>
        {
            SetClient();
            var saved = SessionStore.Load(_api!.Server);
            if (saved is not null)
            {
                RememberBox.IsChecked = true;
                _api.SetToken(saved.Token);
                await EnterWorkspaceAsync(await _api.MeAsync(_lifetime.Token));
            }
            else await ChallengeAsync();
        });
    }

    private void SetClient()
    {
        _api?.Dispose();
        _api = new EtikaApiClient(ServerBox.Text);
        SessionStore.SaveServer(_api.Server);
    }

    private async Task ChallengeAsync()
    {
        _challengeKey = null;
        _challengeServer = null;
        LoginButton.IsEnabled = false;
        CaptchaImage.Source = null;
        CaptchaBox.Text = "";
        var challenge = await _api!.ChallengeAsync(_lifetime.Token);
        if (string.IsNullOrWhiteSpace(challenge.Key) || string.IsNullOrEmpty(challenge.Img)
            || !challenge.Img.StartsWith("data:image/", StringComparison.Ordinal)
            || challenge.Img.Length > 500_000)
            throw new ApiException(502, "Gambar kode verifikasi tidak valid.");
        var comma = challenge.Img.IndexOf(',');
        if (comma < 0) throw new ApiException(502, "Kode verifikasi tidak valid.");
        var bytes = Convert.FromBase64String(challenge.Img[(comma + 1)..]);
        CaptchaImage.Source = await BitmapAsync(bytes);
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
                _challengeKey, CaptchaBox.Text.Trim(), Environment.MachineName, _lifetime.Token);
        }
        catch
        {
            _challengeKey = null;
            try { await ChallengeAsync(); } catch { /* retain the original login failure */ }
            throw;
        }
        PasswordBox.Password = "";
        SessionStore.Clear(_api!.Server);
        if (RememberBox.IsChecked == true)
            SessionStore.Save(_api.Server, new SavedSession(result.Token, result.ExpiresAt));
        await EnterWorkspaceAsync(result.User);
    });

    private async Task EnterWorkspaceAsync(Identity user)
    {
        var context = await _api!.ContextAsync(_lifetime.Token);
        UserName.Text = user.Name;
        UserRole.Text = user.Level;
        YearBox.ItemsSource = context.Tahun;
        YearBox.SelectedItem = context.Tahun.FirstOrDefault(x => x.Tahun == context.TahunAktif)
            ?? context.Tahun.FirstOrDefault();
        LoginPanel.Visibility = Visibility.Collapsed;
        Workspace.Visibility = Visibility.Visible;
        _page = 1;
        await LoadDocumentsAsync();
    }

    private async Task LoadDocumentsAsync()
    {
        ClearDetail();
        DocumentsList.ItemsSource = null;
        if (YearBox.SelectedItem is not BudgetYear year)
        {
            EmptyState.Visibility = Visibility.Visible;
            EmptyState.Text = "Belum ada tahun anggaran tersedia.";
            PageLabel.Text = "";
            PreviousButton.IsEnabled = NextButton.IsEnabled = false;
            return;
        }
        var result = await _api!.DocumentsAsync(year.Tahun, _page, SearchBox.Text.Trim(), _lifetime.Token);
        DocumentsList.ItemsSource = result.Data;
        EmptyState.Visibility = result.Data.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyState.Text = "Belum ada dokumen untuk pilihan ini.";
        _lastPage = result.Meta.LastPage;
        PageLabel.Text = $"Halaman {_page} dari {_lastPage} · {result.Meta.Total} dokumen";
        PreviousButton.IsEnabled = _page > 1;
        NextButton.IsEnabled = _page < _lastPage;
    }

    private void ClearDetail()
    {
        DetailTitle.Text = "Pilih dokumen";
        DetailSubtitle.Text = DetailStatus.Text = DetailBudget.Text = DetailNotes.Text = "";
        AttachmentsList.ItemsSource = null;
    }

    private async void Document_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_busy || DocumentsList.SelectedItem is not DocumentSummary selected) return;
        await RunAsync(async () =>
        {
            ClearDetail();
            var doc = await _api!.DocumentAsync(selected.Id, _lifetime.Token);
            DetailTitle.Text = doc.DisplayTitle;
            DetailSubtitle.Text = doc.DisplaySubtitle;
            DetailStatus.Text = doc.StatusLabel;
            DetailBudget.Text = $"Pagu RKA: {doc.DisplayPagu}";
            DetailNotes.Text = doc.Keterangan ?? "";
            AttachmentsList.ItemsSource = doc.Lampiran;
        });
    }

    private async void Attachment_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string id }) return;
        await RunAsync(async () =>
        {
            var bytes = await _api!.PdfAsync(id, _lifetime.Token);
            var viewer = new PdfWindow();
            _pdfWindows.Add(viewer);
            viewer.Closed += (_, _) => _pdfWindows.Remove(viewer);
            viewer.Activate();
            await viewer.LoadAsync(bytes);
        });
    }

    private async void Year_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_busy || _api?.HasToken != true) return;
        _page = 1;
        await RunAsync(LoadDocumentsAsync);
    }
    private async void Search_Click(object sender, RoutedEventArgs e)
    {
        _page = 1;
        await RunAsync(LoadDocumentsAsync);
    }
    private async void Search_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && !_busy)
        {
            e.Handled = true;
            _page = 1;
            await RunAsync(LoadDocumentsAsync);
        }
    }
    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RunAsync(LoadDocumentsAsync);
    private async void Previous_Click(object sender, RoutedEventArgs e)
    {
        if (_page > 1) _page--;
        await RunAsync(LoadDocumentsAsync);
    }
    private async void Next_Click(object sender, RoutedEventArgs e)
    {
        if (_page < _lastPage) _page++;
        await RunAsync(LoadDocumentsAsync);
    }
    private async void Logout_Click(object sender, RoutedEventArgs e) => await RunAsync(async () =>
    {
        string? warning = null;
        try { await _api!.LogoutAsync(_lifetime.Token); }
        catch { warning = "Sesi di perangkat sudah dihapus. Pencabutan sesi di server belum berhasil."; }
        ResetSession();
        if (warning is not null)
        {
            ErrorBar.Message = warning;
            ErrorBar.IsOpen = true;
        }
        await ChallengeAsync();
    });

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
        DocumentsList.ItemsSource = null;
        YearBox.ItemsSource = null;
        SearchBox.Text = "";
        UserName.Text = UserRole.Text = "";
        Workspace.Visibility = Visibility.Collapsed;
        LoginPanel.Visibility = Visibility.Visible;
        _challengeKey = null;
        LoginButton.IsEnabled = false;
        CaptchaImage.Source = null;
    }

    private async Task RunAsync(Func<Task> operation)
    {
        if (_busy) return;
        _busy = true;
        ErrorBar.IsOpen = false;
        LoginPanel.IsEnabled = Workspace.IsEnabled = false;
        BusyBar.Visibility = Visibility.Visible;
        try { await operation(); }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (ex is ApiException { StatusCode: 401 or 403 }) ResetSession();
            ErrorBar.Message = ex switch
            {
                HttpRequestException => "Server belum dapat dihubungi. Periksa koneksi dan alamat server.",
                OperationCanceledException => "Permintaan terlalu lama. Silakan coba kembali.",
                ApiException or ArgumentException => ex.Message,
                _ => "Operasi belum berhasil. Silakan coba kembali."
            };
            ErrorBar.IsOpen = true;
        }
        finally
        {
            _busy = false;
            LoginPanel.IsEnabled = Workspace.IsEnabled = true;
            BusyBar.Visibility = Visibility.Collapsed;
        }
    }

    internal static async Task<BitmapImage> BitmapAsync(byte[] bytes)
    {
        using var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
        {
            writer.WriteBytes(bytes);
            await writer.StoreAsync();
        }
        stream.Seek(0);
        var image = new BitmapImage();
        await image.SetSourceAsync(stream);
        return image;
    }
}
