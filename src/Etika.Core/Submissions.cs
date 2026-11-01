using System.Globalization;

namespace Etika.Core;

public sealed record Subactivity(string Id, string Kode, string Nama, decimal PaguPpas)
{
    public string Display => $"{Kode} · {Nama}";
}
public sealed record AttachmentDefinition(string Id, string Jenis, string Nama, bool Wajib);
public sealed record SubmissionOptions(string UnitNama, int Tahun, List<Subactivity> Subkegiatan,
    int LastPage, List<AttachmentDefinition> Lampiran, int MaxFileBytes, int MaxTotalBytes);
public sealed record SubmissionEdit(DocumentDetail Document, List<AttachmentDefinition> Lampiran);
public sealed record SubmissionResult(string Id, string Message);
public sealed record SubmissionInput(string RequestId, int Tahun, string? SubkegiatanId, string? Version,
    string Action, string Tanggal, string Waktu, string Jenis, string? Pembiayaan, string PaguRka, string Keterangan);
public sealed record UploadFile(string LampiranId, string FileName, byte[] Bytes);

public static class SubmissionValidation
{
    public const int MaxFileBytes = 5000 * 1024;
    public const int MaxTotalBytes = 25 * 1024 * 1024;
    public static string Rupiah(string value)
    {
        value = value.Trim();
        if (!System.Text.RegularExpressions.Regex.IsMatch(value, @"^(?:0|[1-9][0-9]*|[1-9][0-9]{0,2}(?:\.[0-9]{3})+)$"))
            throw new ArgumentException("Isi pagu dalam rupiah bulat, misalnya 1250000 atau 1.250.000.");
        value = value.Replace(".", "");
        if (value.Length > 15) throw new ArgumentException("Pagu maksimal 15 digit.");
        return decimal.Parse(value, CultureInfo.InvariantCulture).ToString("0", CultureInfo.InvariantCulture);
    }
    public static void Files(IReadOnlyList<UploadFile> files)
    {
        if (files.Select(f => f.LampiranId).Distinct().Count() != files.Count) throw new ArgumentException("Jenis lampiran ganda.");
        long total = 0;
        foreach (var file in files)
        {
            if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
                || file.Bytes.Length < 5 || !file.Bytes.AsSpan(0, 5).SequenceEqual("%PDF-"u8))
                throw new ArgumentException("Lampiran harus berupa berkas PDF.");
            if (file.Bytes.Length > MaxFileBytes) throw new ArgumentException("Maksimal ukuran setiap PDF adalah 5.000 KB.");
            total += file.Bytes.Length;
        }
        if (total > MaxTotalBytes) throw new ArgumentException("Total unggahan maksimal 25 MB.");
    }
}
