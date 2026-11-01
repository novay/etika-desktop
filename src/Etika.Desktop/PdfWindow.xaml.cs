using Etika.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Data.Pdf;
using Windows.Storage.Streams;

namespace Etika.Desktop;

public sealed partial class PdfWindow : Window
{
    private PdfDocument? _document;
    private InMemoryRandomAccessStream? _input;
    private readonly CancellationTokenSource _lifetime = new();
    private uint _page;
    private double _zoom = 1;
    private bool _working, _closed, _cleaned;

    public PdfWindow(string title = "Lampiran PDF", ElementTheme theme = ElementTheme.Default)
    {
        InitializeComponent();
        PdfRoot.RequestedTheme = theme;
        SystemBackdrop = new MicaBackdrop();
        FileTitle.Text = title;
        Title = title + " — ETIKA";
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1100, 900));
        Closed += (_, _) =>
        {
            _closed = true;
            _lifetime.Cancel();
            PageImage.Source = null;
            if (!_working) Cleanup();
        };
    }

    public async Task LoadAsync(byte[] bytes, CancellationToken cancellationToken = default)
    {
        if (_closed || _working || _document is not null) return;
        _working = true;
        SetBusy(true);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        try
        {
            _input = new InMemoryRandomAccessStream();
            using (var writer = new DataWriter(_input.GetOutputStreamAt(0)))
            {
                writer.WriteBytes(bytes);
                await writer.StoreAsync().AsTask(linked.Token);
            }
            _input.Seek(0);
            var document = await PdfDocument.LoadFromStreamAsync(_input).AsTask(linked.Token);
            if (_closed) return;
            if (document.PageCount == 0) throw new InvalidDataException();
            _document = document;
            await RenderCoreAsync(0, 1, linked.Token);
        }
        catch (OperationCanceledException)
        {
            if (!_closed) ShowError("Pembukaan PDF dibatalkan.");
        }
        catch
        {
            if (!_closed) ShowError("PDF belum dapat ditampilkan. Pastikan berkas valid dan tidak dilindungi kata sandi.");
        }
        finally
        {
            _working = false;
            if (_closed) Cleanup();
            else SetBusy(false);
        }
    }

    private async Task RenderAsync(uint page, double zoom)
    {
        if (_working || _closed || _document is null || page >= _document.PageCount) return;
        _working = true;
        SetBusy(true);
        try { await RenderCoreAsync(page, zoom, _lifetime.Token); }
        catch (OperationCanceledException) when (_closed) { }
        catch { if (!_closed) ShowError("Halaman PDF belum dapat ditampilkan. Coba halaman lain atau buka kembali lampiran."); }
        finally
        {
            _working = false;
            if (_closed) Cleanup();
            else SetBusy(false);
        }
    }

    private async Task RenderCoreAsync(uint index, double zoom, CancellationToken ct)
    {
        using var page = _document!.GetPage(index);
        var size = PdfRenderSize.Calculate(page.Size.Width, page.Size.Height, zoom);
        using var output = new InMemoryRandomAccessStream();
        await page.RenderToStreamAsync(output, new PdfPageRenderOptions
        {
            DestinationWidth = size.Width, DestinationHeight = size.Height
        }).AsTask(ct);
        if (_closed) return;
        output.Seek(0);
        var image = new BitmapImage();
        await image.SetSourceAsync(output).AsTask(ct);
        if (_closed) return;
        PageImage.Source = image;
        PageImage.Width = Math.Clamp(page.Size.Width * zoom, 1, 4096);
        _page = index;
        _zoom = zoom;
        PageNumberBox.Maximum = _document.PageCount;
        PageNumberBox.Value = _page + 1;
        PageText.Text = $"Halaman {_page + 1} dari {_document.PageCount}";
        ZoomText.Text = $"{_zoom:P0}";
        Paper.Visibility = Visibility.Visible;
        PdfError.IsOpen = false;
    }
    private void SetBusy(bool busy)
    {
        Toolbar.IsEnabled = !busy && _document is not null;
        PdfBusy.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        PdfPreviousButton.IsEnabled = _page > 0;
        PdfNextButton.IsEnabled = _document is not null && _page + 1 < _document.PageCount;
        PdfZoomOutButton.IsEnabled = _zoom > .5;
        PdfZoomInButton.IsEnabled = _zoom < 2.5;
    }
    private void ShowError(string message)
    {
        PdfError.Message = message;
        PdfError.IsOpen = true;
        if (PageImage.Source is null) PageText.Text = "PDF belum dapat dibuka";
    }
    private void Cleanup()
    {
        if (_cleaned) return;
        _cleaned = true;
        _document = null;
        _input?.Dispose();
        _input = null;
        _lifetime.Dispose();
    }
    private async void Previous_Click(object sender, RoutedEventArgs e)
    {
        if (!_working && _page > 0) await RenderAsync(_page - 1, _zoom);
    }
    private async void Next_Click(object sender, RoutedEventArgs e)
    {
        if (!_working && _document is not null && _page + 1 < _document.PageCount) await RenderAsync(_page + 1, _zoom);
    }
    private async void PageNumber_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs e)
    {
        if (!_working && _document is not null && double.IsFinite(e.NewValue) && e.NewValue >= 1 && e.NewValue <= _document.PageCount)
            await RenderAsync((uint)e.NewValue - 1, _zoom);
    }
    private async void ZoomIn_Click(object sender, RoutedEventArgs e)
    {
        if (!_working) await RenderAsync(_page, Math.Min(2.5, _zoom + .25));
    }
    private async void ZoomOut_Click(object sender, RoutedEventArgs e)
    {
        if (!_working) await RenderAsync(_page, Math.Max(.5, _zoom - .25));
    }
    private async void ResetZoom_Click(object sender, RoutedEventArgs e)
    {
        if (!_working) await RenderAsync(_page, 1);
    }
}
