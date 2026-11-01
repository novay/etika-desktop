# Mengembangkan ETIKA Desktop di Windows

IDE utama: Visual Studio 2026 dengan workload **WinUI application development**. Proyek ini menggunakan .NET 10.

## Membuka dan menjalankan source

1. Buka Visual Studio Installer, pilih Modify, lalu aktifkan WinUI application development. Pastikan .NET SDK 10 tersedia.
2. Clone https://github.com/novay/etika-desktop melalui menu Git > Clone Repository.
3. Buka Etika.Desktop.sln dari root repository.
4. Di Solution Explorer, klik kanan proyek Etika.Desktop dan pilih Set as Startup Project.
5. Pilih konfigurasi Debug dan platform x64 untuk Windows Intel/AMD 64-bit. Platform ARM64 disediakan untuk development Windows ARM.
6. Pilih profil ETIKA Desktop (Unpackaged).
7. Tekan F5 untuk menjalankan debugger. Untuk mengubah UI sambil aplikasi berjalan, pertahankan sesi debugger ini.

Jika Developer Mode diminta oleh toolchain pengembangan, aktifkan melalui pengaturan Windows untuk developer. Paket portable hasil publish tidak memerlukan Visual Studio untuk dijalankan.

## File yang biasa diedit

| File / proyek | Fungsi |
| --- | --- |
| Etika.Desktop.sln | Solution Visual Studio |
| src/Etika.Desktop/MainWindow.xaml | Login, title bar, navigasi, beranda, dokumen, akun, pengaturan |
| src/Etika.Desktop/Styles/Workspace.xaml | Style kartu, caption, dan daftar dokumen |
| src/Etika.Core/WorkspaceNavigation.cs | Menu berdasarkan peran existing |
| src/Etika.Desktop/PreviewData.cs | Data contoh khusus build Debug |
| src/Etika.Desktop/MainWindow.xaml.cs | Interaksi antarmuka |
| src/Etika.Desktop/PdfWindow.xaml | Viewer PDF |
| src/Etika.Core | Kontrak data dan klien API |
| tests/Etika.Core.Tests | Pengujian klien |

Gunakan editor C#/XAML, debugger, dan XAML Hot Reload saat mengembangkan UI. Gunakan panel Git Changes untuk meninjau perubahan, membuat commit, melakukan pull, dan push.

## Jika aplikasi berhenti saat startup

- Saat menjalankan dari Visual Studio, buka Debug > Windows > Exception Settings dan aktifkan break pada Common Language Runtime Exceptions. Jalankan ulang dengan F5 untuk melihat exception.
- Exception startup yang mencapai kode aplikasi dicatat pada %LOCALAPPDATA%\ETIKA\Desktop\Logs\startup.log.
- Workflow Windows menjalankan scripts/Test-Startup.ps1. Paket aplikasi hanya diunggah jika jendela ETIKA Desktop berhasil muncul dan proses tetap berjalan.
- File log tidak mencatat kata sandi atau token. Hindari memasukkan kredensial atau respons autentikasi ke log tambahan.

## Status perbaikan startup

Paket awal berhasil dikompilasi, tetapi proses berhenti saat startup dengan kode 0xC000027B pada pengujian Windows. Paket publish tersebut tidak menyertakan resource PRI milik aplikasi. Konfigurasi build telah diperbarui agar tooling WinUI menyertakan resource tersebut dalam hasil publish. Hasil pengujian perbaikan dicatat pada docs/VALIDATION.md.

Referensi resmi:
- https://learn.microsoft.com/windows/apps/how-tos/hello-world-winui3
- https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/unpackage-winui-app


## Siklus UI dengan XAML Hot Reload

1. Di Visual Studio Windows, pilih **Debug**, platform yang sesuai, dan profil **ETIKA UI Preview**.
2. Tekan **F5**. Aplikasi membuka beranda dengan data sintetis, tanpa login atau koneksi server.
3. Pilih Admin, Eksekutif, SKPD, Berkas, TAPD, atau SKPD|TAPD pada pemilih peran di bagian atas.
4. Buka dokumen contoh untuk meninjau daftar, rincian, dan tombol lampiran. Lampiran contoh tidak membuka PDF produksi.
5. Saat aplikasi tetap berjalan, edit MainWindow.xaml atau Styles/Workspace.xaml. Simpan perubahan dan periksa hasil di jendela aplikasi; bila diperlukan gunakan tombol Apply XAML Hot Reload.
6. Buka **Debug > Windows > Live Visual Tree** dan **Live Property Explorer** untuk memeriksa layout serta properti komponen. Jika tersedia pada versi IDE, gunakan **XAML Live Preview**.
7. Untuk menguji akun/API/PDF sebenarnya, hentikan debugger dan pilih profil **ETIKA Desktop (Unpackaged)**, lalu F5 kembali.

Pastikan **Tools > Options > Debugging > XAML Hot Reload > Enable XAML Hot Reload** aktif. Pada pengaturan .NET Hot Reload, aktifkan penerapan saat file disimpan jika tersedia. Nama kelompok opsi dapat berbeda antar versi Visual Studio. Profil peluncuran proyek menetapkan nativeDebugging=false agar menggunakan managed debugger.

Perubahan umum seperti warna, padding, ukuran, susunan komponen, dan isi template dapat memakai XAML Hot Reload. Perubahan kelas/event handler baru, x:Class, sebagian perubahan x:Bind, perubahan paket, dan beberapa merged dictionary membutuhkan restart atau build ulang. Hot Reload interaktif tetap perlu diverifikasi di instalasi Visual Studio pengguna; CI memverifikasi build, startup, navigasi, serta pemilihan dokumen, bukan sesi edit IDE.

Jangan memakai paket ZIP Release sebagai loop pengembangan. ZIP adalah hasil publish untuk pengujian/distribusi. Saat mengerjakan source lokal, dependensi diunduh saat restore awal dan build berikutnya memakai cache lokal.

## Bekerja dari Mac

Pilihan dengan langkah paling sedikit bila PC Windows sudah tersedia: akses Windows melalui Remote Desktop dari Mac, lalu buka source dan Visual Studio di Windows tersebut. Proses edit, build, debugger, serta jendela ETIKA berjalan di Windows yang sama. Hot Reload tidak dikirim dari proses Visual Studio di macOS.

Alternatifnya, gunakan VM Windows pada Mac yang mendukungnya. Gunakan platform **ARM64** pada Windows ARM (misalnya VM pada Apple Silicon), atau **x64** pada Windows Intel/AMD. Dukungan proyek ARM64 disediakan; workflow CI saat ini memvalidasi x64. Kebutuhan lisensi dan kompatibilitas VM mengikuti penyedia yang dipakai.

Jika tetap mengedit source di Mac, kirim perubahan melalui Git, lalu pull di Windows dan jalankan source dari Visual Studio. Ini menghindari pengunduhan paket publish setiap perubahan, tetapi perubahan C# atau struktur tertentu masih memerlukan build lokal. Untuk perubahan XAML yang langsung terlihat, edit di Visual Studio Windows saat F5 aktif.

## Pemeriksaan pratinjau otomatis

Workflow Windows menjalankan scripts/Test-WorkspacePreview.ps1 pada build Debug. Pemeriksaan memakai Windows UI Automation untuk membuka setiap peran, memeriksa menu, membuka daftar, dan memilih dokumen sintetis. Screenshot disimpan sebagai artifact diagnostik. Build Release diuji dengan variabel pratinjau yang sama untuk memastikan halaman login tetap muncul.

Mode pratinjau diaktifkan melalui ETIKA_UI_PREVIEW pada launchSettings.json dan kode penyedia datanya dibungkus #if DEBUG. Build Release mengabaikan variabel tersebut. Mode ini tidak membuat token, menyimpan kredensial, atau menghubungi API.

Referensi Hot Reload:
- https://learn.microsoft.com/en-us/windows/apps/develop/ui/xaml-runtime-design-tools
- https://learn.microsoft.com/en-us/visualstudio/debugger/hot-reload


## Pemeriksaan cepat Fase 2

Dari PowerShell pada root repository, jalankan `./scripts/Check-DevEnvironment.ps1`. Pemeriksaan ini menemukan SDK, instalasi Visual Studio, dan platform Windows; workload WinUI tetap perlu dipastikan melalui Installer.

Untuk API Laravel pada PC Windows yang sama, pilih profil **ETIKA Local API**. URL development ada pada Properties/launchSettings.json melalui ETIKA_DEV_SERVER; hanya alamat loopback diterima pada override Debug ini. Untuk server lain, gunakan kolom Server ETIKA dengan URL HTTPS yang dapat diakses Windows. Preferensi server yang tersimpan mengalahkan teks default pada XAML; alamat bawaan tunggal berada di Etika.Core/DesktopPreferences.cs.

Uji Hot Reload: jalankan profil UI Preview dengan F5, buka dokumen contoh, ubah Padding pada style EtikaCardStyle, lalu simpan. Periksa perubahan tanpa memulai proses baru. Bila IDE meminta restart untuk perubahan struktural, jalankan ulang F5. Catat versi Visual Studio dan hasilnya pada daftar penerimaan Fase 2.

Tema serta alamat server disimpan pada settings.json lokal. Token hanya disimpan melalui Windows Credential Locker jika Ingat sesi dipilih; kegagalan penyimpanan ditampilkan sebagai peringatan.
