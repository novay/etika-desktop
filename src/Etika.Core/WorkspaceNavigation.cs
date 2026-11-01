namespace Etika.Core;

public sealed record WorkspaceMenu(string Key, string Title, string Description, string Glyph, int? Status = null);

// These entries describe available views. Document authorization always stays on the API.
public sealed record WorkspaceProfile(string RoleLabel, string ScopeDescription, IReadOnlyList<WorkspaceMenu> Documents)
{
    public static WorkspaceProfile ForLevel(string? level)
    {
        var roles = (level ?? "").Split(['|', ','], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.ToUpperInvariant()).Distinct().ToHashSet();
        string[] supported = ["ADMIN", "EKSEKUTIF", "SKPD", "BERKAS", "TAPD"];
        roles.IntersectWith(supported);
        var labels = new Dictionary<string, string>
        {
            ["ADMIN"] = "Admin", ["EKSEKUTIF"] = "Eksekutif", ["SKPD"] = "SKPD",
            ["BERKAS"] = "Berkas", ["TAPD"] = "TAPD"
        };
        var roleLabel = string.Join(" · ", supported.Where(roles.Contains).Select(x => labels[x]));
        List<WorkspaceMenu> menus = [];
        bool broad = roles.Contains("ADMIN") || roles.Contains("EKSEKUTIF");
        if (broad)
            menus.Add(new("all", roles.Contains("ADMIN") ? "Semua dokumen" : "Monitoring dokumen",
                "Telusuri dokumen dan lampiran pada tahun anggaran terpilih.", "\uE8A5"));
        else if (roles.Count > 1)
            menus.Add(new("accessible", "Dokumen sesuai akses",
                "Gabungan dokumen yang dapat dibaca oleh seluruh peran akun Anda.", "\uE8A5"));
        else if (roles.Contains("SKPD"))
            menus.Add(new("own", "Dokumen saya", "Pantau dokumen pengajuan milik akun dan unit Anda.", "\uE8A5"));

        if (roles.Contains("ADMIN") || roles.Contains("BERKAS"))
            menus.Add(new("berkas", "Pemeriksaan berkas", "Dokumen pada tahap pemeriksaan berkas.", "\uE9D5", 1));
        if (roles.Contains("ADMIN") || roles.Contains("TAPD"))
            menus.Add(new("tapd", "Verifikasi TAPD", "Dokumen pada tahap verifikasi TAPD.", "\uE73A", 3));

        return new(roleLabel, broad
            ? "Akses pemantauan lintas unit. Pilih tahun anggaran untuk memulai."
            : "Dokumen mengikuti kepemilikan, unit, dan penugasan tim pada akun Anda.", menus);
    }
}
