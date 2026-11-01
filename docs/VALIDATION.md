# Validasi ETIKA Desktop v0.2.1

Tanggal: 2 November 2026 (WITA). Commit source yang diuji: 801f6176a5705083f5fe7ac032fbe1e5b8ced900.

| Pemeriksaan | Hasil |
| --- | --- |
| API Laravel 10.49.1 / PHP 8.3.6 / SQLite sintetis | 15 tes / 83 assertions lulus ulang secara lokal pada Fase 2, memakai fixture SQLite dan CAPTCHA sebenarnya |
| Klien C# pada Windows | 26 pemeriksaan lulus |
| Publish Release Windows x64 | Lulus |
| Startup paket Release | Jendela ETIKA Desktop muncul dan proses tetap berjalan |
| Build solution Visual Studio, Debug / x64 | Lulus, tanpa error |
| Startup output Debug | Jendela ETIKA Desktop muncul dan proses tetap berjalan |
| Menu, daftar, dan pemilihan rincian pada Debug | Lulus melalui Windows UI Automation untuk Admin, Eksekutif, SKPD, Berkas, TAPD, dan SKPD + TAPD |
| Pratinjau Debug pada Release | Variabel ETIKA_UI_PREVIEW diabaikan; halaman login tetap tampil |
| Login, tahun, pencarian, paginasi, data kosong, refresh dari HTTP fixture | Lulus pada aplikasi Windows Debug |
| Kegagalan server, pembatalan, dan coba lagi | Lulus pada aplikasi Windows Debug |
| Credential Locker / restart / tema dan server tersimpan | Lulus pada runner Windows |
| Sesi berakhir, akun nonaktif, logout dan penutupan viewer | Lulus pada HTTP fixture |
| Rendering PDF dua halaman, navigasi, zoom, PDF rusak/terkunci/hilang/HTML/terlalu besar | Lulus pada aplikasi Windows dengan fixture sintetis |
| XAML Hot Reload interaktif di Visual Studio | Profil disiapkan; sesi edit IDE perlu diverifikasi pada instalasi pengguna |
| UAT seluruh fitur pada perangkat pengguna dan dokumen sebenarnya | Menunggu UAT |
| Integrasi MySQL/storage pada server staging | Belum diuji |
| Windows ARM64 | Konfigurasi source disediakan; belum diuji pada runner ARM64 |

[Hasil build dan pengujian](https://github.com/novay/etika-desktop/actions/runs/41330719597). [Paket publish Windows x64](https://github.com/novay/etika-desktop/actions/runs/41330719597/artifacts/13093375660).

## Perbaikan kegagalan startup

Paket awal lolos kompilasi, tetapi berhenti saat startup dengan kode 0xC000027B. Gejalanya berhasil direproduksi pada runner Windows. Paket publish tidak menyertakan PRI/XBF aplikasi yang diperlukan untuk memuat tampilan.

Konfigurasi EnableMsixTooling diaktifkan sehingga pipeline PRI WinUI menyertakan resource aplikasi pada hasil publish. Pengujian ulang Release serta output Debug berhasil membuka jendela utama.

Etika.Desktop.sln, profil Visual Studio Unpackaged, konfigurasi x64/ARM64, dan pencatatan exception startup ditambahkan. Panduan IDE: [DEVELOPMENT_WINDOWS.md](DEVELOPMENT_WINDOWS.md).

## Batas uji startup

scripts/Test-Startup.ps1 menunggu window handle dengan judul ETIKA Desktop, lalu memastikan proses tetap hidup selama tiga detik. Ini membuktikan inisialisasi dan pembukaan jendela; belum memvalidasi semua interaksi, login terhadap backend, anotasi, maupun isi PDF.

Pengecualian startup yang mencapai kode aplikasi dicatat pada %LOCALAPPDATA%\ETIKA\Desktop\Logs\startup.log. Kegagalan loader sebelum kode aplikasi berjalan dapat memerlukan Windows Event Viewer.

## Backend dan distribusi

Backend: https://github.com/novay/etika.kukarkab.com/pull/1 sudah digabung ke main. Penerapan dan aktivasi API pada server tujuan dikelola terpisah dari pekerjaan UI desktop ini. Harness backend memakai data sintetis; dump produksi tidak disertakan pada repository.

Paket desktop adalah folder publish portable. Ekstrak seluruh ZIP ke folder baru dan pertahankan file pendukung. Installer dan penandatanganan belum dibuat.

## Kesiapan layanan login

Versi 0.1.2 membedakan HTTP 404 pada endpoint login/challenge dari kesalahan dokumen. Pesan teknis route server diganti dengan petunjuk untuk memeriksa alamat server atau menghubungi pengelola ETIKA. UI menunggu CAPTCHA valid sebelum mengaktifkan Masuk. Pengujian klien tambahan memastikan kredensial tidak dikirim sebelum challenge siap.

Perbaikan penanganan ini tidak memasang API pada server web. Penerapan backend dan pengaktifan konfigurasinya tetap diperlukan agar login berfungsi.


## Ruang kerja v0.2.0

Title bar, pencarian, navigasi peran, beranda, kartu dokumen, rincian adaptif, akun, dan tema ditambahkan. Empat pemeriksaan klien baru memvalidasi menu peran tunggal, normalisasi beberapa peran, penolakan peran tidak dikenal, dan pengiriman filter status bersama pencarian/paginasi. Akses dokumen tetap diperiksa backend.

Pratinjau Debug memakai data sintetis tanpa API atau penyimpanan sesi. UI Automation memeriksa menu yang ada/tidak ada, membuka daftar, dan memilih dokumen untuk enam kombinasi peran. Screenshot beranda dan rincian disimpan pada artifact diagnostik workflow. Screenshot beranda serta rincian ditinjau untuk tata letak dasar; skala layar, tema gelap, dan Hot Reload interaktif memerlukan pemeriksaan manual sesuai WINDOWS_ACCEPTANCE.md.


## Hasil Fase 2 / v0.2.1

Workflow Windows berhasil membangun Release dan Debug x64, membuka keduanya, memeriksa enam kombinasi menu peran, memastikan Release tetap menampilkan login, serta menjalankan Test-ApiIntegration.ps1. Integrasi sintetis menguji login salah/benar, CAPTCHA baru, tahun, pencarian, paginasi, hasil kosong, perubahan server yang dibaca ulang, pembatalan, retry, Credential Locker setelah restart, tema gelap yang tersimpan, PDF native dua halaman, navigasi/zoom, berkas rusak/terkunci/hilang/HTML/terlalu besar, logout, sesi kedaluwarsa, akun nonaktif, dan login/daftar/detail/logout lima kombinasi peran tambahan. Tidak ada kasus yang dilewati pada eksekusi terakhir.

Screenshot tema gelap setelah restart telah diperiksa. Rendering PDF dibuktikan lewat Windows.Data.Pdf, pemuatan BitmapImage, serta pemeriksaan navigasi dan zoom melalui UI Automation; pemeriksaan visual lengkap terhadap dokumen pengguna tetap merupakan UAT.

Endpoint publik produksi GET /api/v1/auth/challenge juga diperiksa pada Fase 2: HTTP 200, data.key tersedia, dan data.img berisi data URI gambar. Tidak ada login produksi atau permintaan data dokumen berautentikasi yang dilakukan dalam pemeriksaan publik ini.

Harness Laravel diulang pada PHP 8.3.6: 15 tes / 83 assertions lulus, memakai SQLite sintetis. Server HTTP fixture Windows hanya membuktikan perilaku klien/protokol dan tidak menggantikan pengujian Laravel + MySQL + storage tujuan.

Status: implementasi dan pemeriksaan otomatis Fase 2 selesai; penerimaan lingkungan pengguna belum ditutup. Matriks yang perlu diisi ada pada [PHASE2_UAT.md](PHASE2_UAT.md). Ringkasan implementasi: [PHASE2.md](PHASE2.md).
