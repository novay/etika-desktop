using System.Globalization;
using Etika.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.Storage.Pickers;

namespace Etika.Desktop;

public sealed partial class SubmissionWindow : Window
{
    private readonly EtikaApiClient _api;
    private readonly int _year;
    private readonly string? _id;
    private readonly CancellationTokenSource _lifetime = new();
    private CancellationTokenSource? _operation;
    private DocumentDetail? _document;
    private List<AttachmentDefinition> _definitions = [];
    private readonly Dictionary<string, UploadFile> _uploads = [];
    private SubmissionInput? _pending;
    private List<UploadFile> _pendingFiles = [];
    private bool _busy, _ready, _loaded;
    private int _page = 1, _lastPage = 1;
    public event Action<SubmissionResult>? Submitted;
    public event Action? SessionExpired;

    public SubmissionWindow(EtikaApiClient api, int year, string? id, ElementTheme theme)
    {
        _api = api; _year = year; _id = id;
        InitializeComponent();
        Root.RequestedTheme = theme;
        SystemBackdrop = new MicaBackdrop();
        AppWindow.Resize(new Windows.Graphics.SizeInt32(820, 900));
        CategoryBox.ItemsSource = new[] { "Belanja", "Pendapatan", "Pembiayaan" };
        FinancingBox.ItemsSource = new[] { "Penerimaan", "Pengeluaran" };
        var wita = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8));
        DateBox.Date = wita;
        TimeBox.Time = wita.TimeOfDay;
        Closed += (_, _) => { _lifetime.Cancel(); _uploads.Clear(); _pendingFiles.Clear(); };
    }

    private async void Root_Loaded(object sender, RoutedEventArgs e)
    {
        if (_loaded) return;
        _loaded = true;
        await RunAsync(LoadAsync);
    }
    private async Task LoadAsync(CancellationToken ct)
    {
        if (_id is null)
        {
            var options = await _api.SubmissionOptionsAsync(_year, _page, SearchBox.Text.Trim(), ct);
            _definitions = options.Lampiran;
            ContextText.Text = $"{options.UnitNama} · Tahun {options.Tahun}";
            SubList.ItemsSource = options.Subkegiatan;
            _lastPage = options.LastPage;
            PageText.Text = options.Subkegiatan.Count == 0 ? "Tidak ada subkegiatan tersedia" : $"Halaman {_page} dari {_lastPage}";
            PreviousButton.IsEnabled = _page > 1; NextButton.IsEnabled = _page < _lastPage;
            if (CategoryBox.SelectedItem is null) CategoryBox.SelectedItem = "Belanja";
        }
        else
        {
            var edit = await _api.SubmissionEditAsync(_id, ct);
            _document = edit.Document; _definitions = edit.Lampiran;
            Heading.Text = _document.Status == 2 ? "Perbaiki pengajuan" : "Sunting pengajuan";
            ContextText.Text = _document.DisplaySubtitle;
            SubPicker.Visibility = Visibility.Collapsed;
            SelectedSubText.Text = _document.DisplayTitle;
            PpasText.Text = "Pagu PPAS: " + _document.PaguPpas.ToString("C0", CultureInfo.GetCultureInfo("id-ID"));
            DateBox.Date = DateTimeOffset.TryParse(_document.Tanggal, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;
            TimeBox.Time = TimeSpan.TryParse(_document.Waktu, CultureInfo.InvariantCulture, out var time) ? time : TimeSpan.Zero;
            BudgetBox.Text = _document.PaguRka.ToString("0", CultureInfo.InvariantCulture);
            NotesBox.Text = _document.Keterangan ?? "";
            FinancingBox.SelectedItem = _document.Pembiayaan;
            CategoryBox.SelectedItem = _document.Jenis;
            CategoryBox.IsEnabled = _document.Lampiran.Count == 0;
            RevisionBar.Message = _document.CatatanRevisi ?? "";
            RevisionBar.IsOpen = _document.Status == 2 && !string.IsNullOrWhiteSpace(_document.CatatanRevisi);
            SaveButton.Content = "Simpan perubahan";
            ResubmitButton.Visibility = _document.Status == 2 ? Visibility.Visible : Visibility.Collapsed;
        }
        RenderFiles();
        _ready = true;
    }
    private void Sub_Changed(object sender, SelectionChangedEventArgs e)
    {
        var sub = SubList.SelectedItem as Subactivity;
        SelectedSubText.Text = sub?.Display ?? "Pilih subkegiatan yang akan diajukan.";
        PpasText.Text = sub is null ? "" : "Pagu PPAS: " + sub.PaguPpas.ToString("C0", CultureInfo.GetCultureInfo("id-ID"));
    }
    private void Category_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (FinancingBox is null) return;
        FinancingBox.Visibility = CategoryBox.SelectedItem as string == "Pembiayaan" ? Visibility.Visible : Visibility.Collapsed;
        RenderFiles();
    }
    private void RenderFiles()
    {
        if (FilesPanel is null) return;
        FilesPanel.Children.Clear();
        foreach (var definition in _definitions.Where(x => x.Jenis == CategoryBox.SelectedItem as string))
        {
            var panel = new StackPanel { Spacing = 6 };
            panel.Children.Add(new TextBlock { Text = definition.Nama + (definition.Wajib ? " *" : ""), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
            var old = _document?.Lampiran.FirstOrDefault(x => x.LampiranId == definition.Id);
            var label = new TextBlock { Text = _uploads.TryGetValue(definition.Id, out var upload) ? upload.FileName
                : old is null ? "Belum dipilih" : $"Tersimpan: {old.Nama} ({old.Size / 1024:N0} KB)", TextWrapping = TextWrapping.Wrap };
            panel.Children.Add(label);
            var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            var pick = new Button { Content = "Pilih PDF", Tag = definition.Id };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(pick, "Pick_" + definition.Id);
            pick.Click += Pick_Click;
            actions.Children.Add(pick);
            var clear = new Button { Content = "Batalkan pilihan", Tag = definition.Id };
            clear.Click += (_, _) => { _uploads.Remove(definition.Id); RenderFiles(); };
            actions.Children.Add(clear);
            panel.Children.Add(actions); FilesPanel.Children.Add(panel);
        }
    }
    private async void Pick_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || sender is not Button { Tag: string key }) return;
        await RunAsync(async ct =>
        {
            var picker = new FileOpenPicker(AppWindow.Id);
            picker.FileTypeFilter.Add(".pdf");
            var result = await picker.PickSingleFileAsync();
            if (result is null || ct.IsCancellationRequested) return;
            await using var file = new FileStream(result.Path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (file.Length > SubmissionValidation.MaxFileBytes) throw new ArgumentException("Maksimal ukuran setiap PDF adalah 5.000 KB.");
            using var bytes = new MemoryStream();
            var buffer = new byte[8192]; int read;
            while ((read = await file.ReadAsync(buffer, ct)) > 0)
            {
                if (bytes.Length + read > SubmissionValidation.MaxFileBytes) throw new ArgumentException("Ukuran PDF melebihi batas.");
                bytes.Write(buffer, 0, read);
            }
            var upload = new UploadFile(key, Path.GetFileName(result.Path), bytes.ToArray());
            var candidate = _uploads.Values.Where(x => x.LampiranId != key).Append(upload).ToList();
            SubmissionValidation.Files(candidate);
            _uploads[key] = upload; RenderFiles();
        });
    }
    private async void Search_Click(object sender, RoutedEventArgs e) { _page = 1; await RunAsync(LoadAsync); }
    private async void Previous_Click(object sender, RoutedEventArgs e) { if (_page > 1) { _page--; await RunAsync(LoadAsync); } }
    private async void Next_Click(object sender, RoutedEventArgs e) { if (_page < _lastPage) { _page++; await RunAsync(LoadAsync); } }
    private async void Save_Click(object sender, RoutedEventArgs e) => await ConfirmAsync(_id is null ? "submit" : "save");
    private async void Resubmit_Click(object sender, RoutedEventArgs e) => await ConfirmAsync("resubmit");
    private async Task ConfirmAsync(string action)
    {
        if (_busy || !_ready || _pending is not null) return;
        await RunAsync(async ct =>
        {
            if (_id is null && SubList.SelectedItem is not Subactivity) throw new ArgumentException("Pilih subkegiatan terlebih dahulu.");
            if (DateBox.Date is null || TimeBox.Time < TimeSpan.Zero) throw new ArgumentException("Isi tanggal dan waktu penyerahan.");
            var category = CategoryBox.SelectedItem as string ?? throw new ArgumentException("Pilih kategori.");
            if (category == "Pembiayaan" && FinancingBox.SelectedItem is null) throw new ArgumentException("Pilih jenis pembiayaan.");
            var definitions = _definitions.Where(x => x.Jenis == category).ToList();
            foreach (var definition in definitions.Where(x => x.Wajib))
                if (!_uploads.ContainsKey(definition.Id) && _document?.Lampiran.Any(x => x.LampiranId == definition.Id) != true)
                    throw new ArgumentException("Unggah lampiran wajib: " + definition.Nama + ".");
            var files = _uploads.Values.Where(x => definitions.Any(d => d.Id == x.LampiranId)).ToList();
            SubmissionValidation.Files(files);
            var amount = SubmissionValidation.Rupiah(BudgetBox.Text);
            var input = new SubmissionInput(Guid.NewGuid().ToString(), _year, (SubList.SelectedItem as Subactivity)?.Id,
                _document?.Version, action, DateBox.Date.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                TimeBox.Time.ToString(@"hh\:mm", CultureInfo.InvariantCulture), category,
                category == "Pembiayaan" ? FinancingBox.SelectedItem as string : null, amount, NotesBox.Text.Trim());
            var dialog = new ContentDialog {
                XamlRoot = Root.XamlRoot, RequestedTheme = Root.ActualTheme,
                Title = action == "save" ? "Simpan perubahan?" : action == "resubmit" ? "Kirim ulang dokumen?" : "Kirim pengajuan?",
                Content = $"{SelectedSubText.Text}\nTahun {_year} · {category}\nPagu RKA: Rp{decimal.Parse(amount).ToString("N0", CultureInfo.GetCultureInfo("id-ID"))}\n{files.Count} PDF baru atau pengganti.\n\n" +
                    (action == "save" ? "Status dokumen tetap sama." : "Pastikan isian dan lampiran benar. Dokumen akan masuk ke pemeriksaan berkas."),
                PrimaryButtonText = action == "save" ? "Simpan" : "Kirim", CloseButtonText = "Periksa kembali", DefaultButton = ContentDialogButton.Close
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary || ct.IsCancellationRequested) return;
            _pending = input; _pendingFiles = files;
            await SendPendingAsync(ct);
        });
    }
    private async Task SendPendingAsync(CancellationToken ct)
    {
        var result = await _api.SubmitAsync(_id, _pending!, _pendingFiles, ct);
        _pending = null; _pendingFiles.Clear();
        Submitted?.Invoke(result);
        Close();
    }
    private async void Retry_Click(object sender, RoutedEventArgs e) => await RunAsync(_pending is null ? LoadAsync : SendPendingAsync);
    private void Cancel_Click(object sender, RoutedEventArgs e) => _operation?.Cancel();
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private async Task RunAsync(Func<CancellationToken, Task> operation)
    {
        if (_busy || _lifetime.IsCancellationRequested) return;
        _busy = true; MessageBar.IsOpen = false; RetryButton.Visibility = Visibility.Collapsed;
        _operation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        SetBusy(true);
        try { await operation(_operation.Token); }
        catch (Exception error) when (!_lifetime.IsCancellationRequested)
        {
            if (error is ApiException { StatusCode: 401 or 403 }) { SessionExpired?.Invoke(); Close(); return; }
            bool uncertain = _pending is not null && (error is HttpRequestException or OperationCanceledException || error is ApiException { StatusCode: >= 500 });
            if (!uncertain) { _pending = null; _pendingFiles.Clear(); }
            MessageBar.Message = error switch {
                ApiException or ArgumentException => error.Message,
                OperationCanceledException => "Permintaan terhenti.",
                _ => "Operasi belum berhasil. Periksa koneksi atau berkas Anda."
            };
            MessageBar.IsOpen = true;
            if (error is ApiException { StatusCode: 409 }) _ready = false;
            RetryButton.Visibility = uncertain || !_ready ? Visibility.Visible : Visibility.Collapsed;
            StateText.Text = uncertain ? "Hasil pengiriman belum diketahui. Coba lagi memakai permintaan yang sama; isian dikunci agar tidak terkirim ganda. Jika menutup jendela, periksa daftar dokumen sebelum membuat pengajuan baru." : "";
        }
        catch (Exception) when (_lifetime.IsCancellationRequested) { }
        finally
        {
            _operation.Dispose(); _operation = null; _busy = false;
            if (!_lifetime.IsCancellationRequested) SetBusy(false);
        }
    }
    private void SetBusy(bool busy)
    {
        FormPanel.IsEnabled = !busy && _pending is null;
        SaveButton.IsEnabled = ResubmitButton.IsEnabled = !busy && _ready && _pending is null;
        RetryButton.IsEnabled = !busy;
        CancelButton.Visibility = BusyBar.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
    }
}
