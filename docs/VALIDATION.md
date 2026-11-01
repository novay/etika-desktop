# Validasi Fase 1

Tanggal: 1 November 2026. Baseline backend: e265090ce54c912abbb169a8b7789309df2ce99d.

| Pemeriksaan | Hasil |
| --- | --- |
| API Laravel 10.49.1, PHP 8.3.6, SQLite sintetis | 15 tes / 83 assertions lulus |
| Workflow API di GitHub Actions | Lulus: https://github.com/novay/etika.kukarkab.com/actions/runs/41277406920 |
| Klien C# | 15 pemeriksaan lulus di Linux dan runner Windows |
| Kompilasi Etika.Core | Lulus |
| Pemeriksaan semantik C# UI terhadap referensi WinUI | Lulus dengan stub sementara untuk field yang biasanya dihasilkan XAML |
| XML/XAML dan nama event handler | Struktur terbaca, semua handler yang dirujuk tersedia |
| Pemulihan dependensi proyek WinUI | Lulus |
| Build penuh WinUI | Lulus pada runner Windows x64: https://github.com/novay/etika-desktop/actions/runs/41277728059 |
| Artifact publish Windows x64 | Tersedia: https://github.com/novay/etika-desktop/actions/runs/41277728059/artifacts/13071587759 |
| Menjalankan UI dan render PDF di Windows | Belum dilakukan |
| Integrasi server ETIKA produksi/staging | Belum dilakukan |
| Regression test seluruh web existing / MySQL | Belum dilakukan |

Pemeriksaan semantik C# UI bukan build XAML dan bukan uji tampilan. Stub disimpan sementara di luar source deliverable; tidak ikut ZIP atau proyek Windows.

Cakupan API: akses lintas unit/pengguna dan file, antrean tim/stage Berkas/TAPD, peran Admin/Eksekutif dan peran tidak dikenal, akun nonaktif, token salah kemampuan/kedaluwarsa, penolakan cookie web, pencabutan token perangkat saja, identitas tanpa field sensitif, pilihan tahun, filter/paginasi, data terhapus, PDF sah/palsu/terlalu besar/hilang, traversal storage, CAPTCHA nyata dengan jawaban salah, login dengan validasi CAPTCHA deterministik, input bertipe tidak benar, dan lockout percobaan gagal.

Cakupan klien: server HTTP tidak sah, normalisasi URL API/subdirektori, encoding pencarian, header Bearer, 401, logout saat offline, ketepatan ID/rupiah besar, redirect, signature dan ukuran PDF, JSON tidak valid, payload login, serta penghapusan token lama pada login gagal.

Satu peringatan libpng pada gambar latar CAPTCHA bawaan dependency muncul ketika pengujian challenge; tes tetap lulus.

Workflow Windows telah dijalankan untuk commit c6c58f951c86fa6990a5138363041397760bd7da dan berhasil menghasilkan paket publish x64. Build Linux sebelumnya berhenti pada tool XAML/MakePRI khusus Windows; hambatan kompilasi tersebut sudah diselesaikan melalui runner Windows. Pengujian tampilan interaktif, render PDF, dan koneksi staging tetap belum dilakukan. Installer dan penandatanganan belum tersedia.

Backend review: https://github.com/novay/etika.kukarkab.com/pull/1
