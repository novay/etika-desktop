# Validasi ETIKA Desktop v0.2.0

Tanggal: 1 November 2026 (WITA). Commit source yang diuji: aabfe8fe094f97d2ce8476d4c521df009dec1688.

| Pemeriksaan | Hasil |
| --- | --- |
| API Laravel 10.49.1 / PHP 8.3.6 / SQLite sintetis | 15 tes / 83 assertions lulus pada validasi backend sebelumnya |
| Klien C# pada Windows | 20 pemeriksaan lulus |
| Publish Release Windows x64 | Lulus |
| Startup paket Release | Jendela ETIKA Desktop muncul dan proses tetap berjalan |
| Build solution Visual Studio, Debug / x64 | Lulus, tanpa error |
| Startup output Debug | Jendela ETIKA Desktop muncul dan proses tetap berjalan |
| Menu, daftar, dan pemilihan rincian pada Debug | Lulus melalui Windows UI Automation untuk Admin, Eksekutif, SKPD, Berkas, TAPD, dan SKPD + TAPD |
| Pratinjau Debug pada Release | Variabel ETIKA_UI_PREVIEW diabaikan; halaman login tetap tampil |
| XAML Hot Reload interaktif di Visual Studio | Profil disiapkan; sesi edit IDE perlu diverifikasi pada instalasi pengguna |
| Interaksi seluruh fitur UI dan render PDF | Belum diuji menyeluruh |
| Integrasi MySQL/storage pada server staging | Belum diuji |
| Windows ARM64 | Konfigurasi source disediakan; belum diuji pada runner ARM64 |

[Hasil build dan pengujian](https://github.com/novay/etika-desktop/actions/runs/41325353505). [Paket publish Windows x64](https://github.com/novay/etika-desktop/actions/runs/41325353505/artifacts/13090398862).

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
