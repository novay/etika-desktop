using System.Globalization;

namespace Etika.Core;

public sealed record Identity(string Id, string Name, string Email, string Level);
public sealed record CaptchaChallenge(string Key, string Img);
public sealed record LoginResult(string Token, DateTimeOffset ExpiresAt, Identity User);
public sealed record BudgetYear(int Tahun, bool Aktif, bool Perubahan);
public sealed record ServerContext(int? TahunAktif, List<BudgetYear> Tahun, bool PengajuanEnabled = false);
public sealed record Attachment(string Id, string Nama, long Size, string? LampiranId = null);
public sealed record PageMeta(int CurrentPage, int LastPage, int Total, int PerPage);
public sealed record DataEnvelope<T>(T Data);
public sealed record PageResult<T>(List<T> Data, PageMeta Meta);

public class DocumentSummary
{
    public string Id { get; set; } = "";
    public int Tahun { get; set; }
    public string Jenis { get; set; } = "";
    public string? Tanggal { get; set; }
    public string UnitKode { get; set; } = "";
    public string? UnitNama { get; set; }
    public string? UnitSingkatan { get; set; }
    public string SubkegiatanKode { get; set; } = "";
    public string? SubkegiatanNama { get; set; }
    public decimal PaguPpas { get; set; }
    public decimal PaguRka { get; set; }
    public int Status { get; set; }
    public int StatusVerif { get; set; }
    public string StatusLabel { get; set; } = "";
    public string? UpdatedAt { get; set; }
    public string DisplayTitle => SubkegiatanNama ?? SubkegiatanKode;
    public string DisplaySubtitle => $"{UnitSingkatan ?? UnitNama ?? UnitKode} · {Jenis} · {Tahun}";
    public string DisplayPagu => PaguRka.ToString("C0", CultureInfo.GetCultureInfo("id-ID"));
}

public sealed class DocumentDetail : DocumentSummary
{
    public bool CanEdit { get; set; }
    public string? Waktu { get; set; }
    public string? Pembiayaan { get; set; }
    public string? Version { get; set; }
    public string? CatatanRevisi { get; set; }
    public string? Keterangan { get; set; }
    public List<Attachment> Lampiran { get; set; } = [];
}

public sealed class ApiException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
