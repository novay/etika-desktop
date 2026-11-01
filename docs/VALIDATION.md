# Validasi ETIKA Desktop v0.1.1

Tanggal: 1 November 2026 (WITA). Commit source yang diuji: 648b7ed8a45fac8a04fe4cddbea6bda625dadf1d.

| Pemeriksaan | Hasil |
| --- | --- |
| API Laravel 10.49.1 / PHP 8.3.6 / SQLite sintetis | 15 tes / 83 assertions lulus pada validasi backend sebelumnya |
| Klien C# pada Windows | 15 pemeriksaan lulus |
| Publish Release Windows x64 | Lulus |
| Startup paket Release | Jendela ETIKA Desktop muncul dan proses tetap berjalan |
| Build solution Visual Studio, Debug / x64 | Lulus, tanpa error |
| Startup output Debug | Jendela ETIKA Desktop muncul dan proses tetap berjalan |
| Interaksi seluruh fitur UI dan render PDF | Belum diuji menyeluruh |
| Integrasi MySQL/storage pada server staging | Belum diuji |
| Windows ARM64 | Konfigurasi source disediakan; belum diuji pada runner ARM64 |

[Hasil build dan pengujian](https://github.com/novay/etika-desktop/actions/runs/41281598091). [Paket publish Windows x64](https://github.com/novay/etika-desktop/actions/runs/41281598091/artifacts/13074100028).

## Perbaikan kegagalan startup

Paket awal lolos kompilasi, tetapi berhenti saat startup dengan kode 0xC000027B. Gejalanya berhasil direproduksi pada runner Windows. Paket publish tidak menyertakan PRI/XBF aplikasi yang diperlukan untuk memuat tampilan.

Konfigurasi EnableMsixTooling diaktifkan sehingga pipeline PRI WinUI menyertakan resource aplikasi pada hasil publish. Pengujian ulang Release serta output Debug berhasil membuka jendela utama.

Etika.Desktop.sln, profil Visual Studio Unpackaged, konfigurasi x64/ARM64, dan pencatatan exception startup ditambahkan. Panduan IDE: [DEVELOPMENT_WINDOWS.md](DEVELOPMENT_WINDOWS.md).

## Batas uji startup

scripts/Test-Startup.ps1 menunggu window handle dengan judul ETIKA Desktop, lalu memastikan proses tetap hidup selama tiga detik. Ini membuktikan inisialisasi dan pembukaan jendela; belum memvalidasi semua interaksi, login terhadap backend, anotasi, maupun isi PDF.

Pengecualian startup yang mencapai kode aplikasi dicatat pada %LOCALAPPDATA%\ETIKA\Desktop\Logs\startup.log. Kegagalan loader sebelum kode aplikasi berjalan dapat memerlukan Windows Event Viewer.

## Backend dan distribusi

Backend review: https://github.com/novay/etika.kukarkab.com/pull/1. API belum diterapkan ke produksi melalui pekerjaan desktop ini. Harness backend memakai data sintetis; dump produksi tidak disertakan pada repository.

Paket desktop adalah folder publish portable. Ekstrak seluruh ZIP ke folder baru dan pertahankan file pendukung. Installer dan penandatanganan belum dibuat.
