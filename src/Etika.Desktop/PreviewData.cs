#if DEBUG
using Etika.Core;

namespace Etika.Desktop;

// Compiled only into Debug. Preview never creates an API client or saves credentials.
internal static class PreviewData
{
    public static Identity User(string role) => new("preview", "Pengguna Contoh", "contoh@etika.test", role);
    public static ServerContext Context => new(2026, [new(2026, true, false), new(2025, false, false)]);

    public static PageResult<DocumentSummary> Documents(WorkspaceProfile profile, int year, int page, string search, int? status)
    {
        string[] titles = ["Penyusunan dokumen perencanaan perangkat daerah", "Koordinasi pelaksanaan kegiatan kepemudaan",
            "Pengelolaan sarana dan prasarana olahraga", "Peningkatan kapasitas organisasi kepemudaan",
            "Penyusunan laporan keuangan perangkat daerah", "Pelaksanaan pembinaan olahraga prestasi"];
        int[] stages = [1, 3, 2, 3, 5, 1];
        string[] labels = ["Pemeriksaan berkas", "Verifikasi TAPD", "Revisi", "Verifikasi TAPD", "Selesai", "Pemeriksaan berkas"];
        bool reviewerOnly = profile.RoleLabel is "Berkas" or "TAPD";
        var documents = titles.Select((title, i) => new DocumentSummary
        {
            Id = $"contoh-{i}", Tahun = year, Jenis = "Belanja", SubkegiatanKode = $"2.19.01.{i + 1:D3}",
            SubkegiatanNama = title, UnitKode = "UNIT-CONTOH", UnitNama = "Perangkat Daerah Contoh",
            UnitSingkatan = i % 2 == 0 ? "UNIT A (contoh)" : "UNIT B (contoh)",
            PaguRka = 125_000_000m + i * 42_500_000m, Status = stages[i], StatusVerif = i == 4 ? 1 : 0,
            StatusLabel = labels[i]
        }).Where(x => (!reviewerOnly || x.Status == profile.Documents[0].Status)
            && (status is null || x.Status == status)
            && (x.DisplayTitle.Contains(search, StringComparison.OrdinalIgnoreCase) || x.SubkegiatanKode.Contains(search, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        return new(documents.Skip((page - 1) * 20).Take(20).ToList(), new(page, Math.Max(1, (documents.Count + 19) / 20), documents.Count, 20));
    }

    public static DocumentDetail Detail(DocumentSummary doc) => new()
    {
        Id = doc.Id, Tahun = doc.Tahun, Jenis = doc.Jenis, SubkegiatanKode = doc.SubkegiatanKode,
        SubkegiatanNama = doc.SubkegiatanNama, UnitKode = doc.UnitKode, UnitNama = doc.UnitNama,
        UnitSingkatan = doc.UnitSingkatan, PaguRka = doc.PaguRka, Status = doc.Status,
        StatusVerif = doc.StatusVerif, StatusLabel = doc.StatusLabel,
        Keterangan = "Data contoh untuk meninjau layout, tema, dan menu. Tidak terhubung dengan dokumen produksi.",
        Lampiran = [new("contoh-kak", "Kerangka Acuan Kerja (KAK)", 0), new("contoh-rka", "RKA / DPA Belanja", 0)]
    };
}
#endif
