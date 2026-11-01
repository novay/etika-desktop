using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Data.Pdf;
using Windows.Storage.Streams;

namespace Etika.Desktop;

public sealed partial class PdfWindow : Window
{
    private PdfDocument? _document;
    private InMemoryRandomAccessStream? _input;
    private uint _page;
    private double _zoom = 1;
    private bool _rendering;
    private bool _closed;

    public PdfWindow()
    {
        InitializeComponent();
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1000, 850));
        Closed += (_, _) =>
        {
            _closed = true;
            PageImage.Source = null;
            _document = null;
            if (!_rendering) { _input?.Dispose(); _input = null; }
        };
    }

    public async Task LoadAsync(byte[] bytes)
    {
        try
        {
            Toolbar.IsEnabled = false;
            _input = new InMemoryRandomAccessStream();
            using (var writer = new DataWriter(_input.GetOutputStreamAt(0)))
            {
                writer.WriteBytes(bytes);
                await writer.StoreAsync();
            }
            _input.Seek(0);
            _document = await PdfDocument.LoadFromStreamAsync(_input);
            if (_closed) return;
            await RenderAsync();
        }
        catch
        {
            if (!_closed)
            {
                PdfError.Message = "PDF belum dapat ditampilkan. Pastikan berkas valid dan tidak dilindungi kata sandi.";
                PdfError.IsOpen = true;
            }
        }
    }

    private async Task RenderAsync()
    {
        if (_rendering || _closed || _document is null || _document.PageCount == 0) return;
        _rendering = true;
        Toolbar.IsEnabled = false;
        try
        {
            using var page = _document.GetPage(_page);
            using var output = new InMemoryRandomAccessStream();
            await page.RenderToStreamAsync(output, new PdfPageRenderOptions
            {
                DestinationWidth = (uint) Math.Clamp(1200 * _zoom, 600, 3000)
            });
            if (_closed) return;
            output.Seek(0);
            var image = new BitmapImage();
            await image.SetSourceAsync(output);
            if (_closed) return;
            PageImage.Source = image;
            PageImage.Width = page.Size.Width * _zoom;
            PageText.Text = $"{_page + 1} / {_document.PageCount}";
            ZoomText.Text = $"{_zoom:P0}";
        }
        catch
        {
            if (!_closed) { PdfError.Message = "Halaman PDF belum dapat dirender."; PdfError.IsOpen = true; }
        }
        finally
        {
            _rendering = false;
            if (_closed) { _input?.Dispose(); _input = null; }
            else Toolbar.IsEnabled = true;
        }
    }
    private async void Previous_Click(object sender, RoutedEventArgs e)
    {
        if (_page > 0) _page--;
        await RenderAsync();
    }
    private async void Next_Click(object sender, RoutedEventArgs e)
    {
        if (_document is not null && _page + 1 < _document.PageCount) _page++;
        await RenderAsync();
    }
    private async void ZoomIn_Click(object sender, RoutedEventArgs e)
    {
        _zoom = Math.Min(2.5, _zoom + .25);
        await RenderAsync();
    }
    private async void ZoomOut_Click(object sender, RoutedEventArgs e)
    {
        _zoom = Math.Max(.5, _zoom - .25);
        await RenderAsync();
    }
}
