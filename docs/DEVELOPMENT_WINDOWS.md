# Mengembangkan ETIKA Desktop di Windows

IDE utama: Visual Studio 2026 dengan workload **WinUI application development**. Proyek ini menggunakan .NET 10.

## Membuka dan menjalankan source

1. Buka Visual Studio Installer, pilih Modify, lalu aktifkan WinUI application development. Pastikan .NET SDK 10 tersedia.
2. Clone https://github.com/novay/etika-desktop melalui menu Git > Clone Repository.
3. Buka Etika.Desktop.sln dari root repository.
4. Di Solution Explorer, klik kanan proyek Etika.Desktop dan pilih Set as Startup Project.
5. Pilih konfigurasi Debug dan platform x64 untuk Windows Intel/AMD 64-bit. Platform ARM64 disediakan untuk development Windows ARM.
6. Pilih profil ETIKA Desktop (Unpackaged).
7. Tekan F5 untuk menjalankan debugger. Gunakan Ctrl+F5 untuk menjalankan tanpa debugger.

Jika Developer Mode diminta oleh toolchain pengembangan, aktifkan melalui pengaturan Windows untuk developer. Paket portable hasil publish tidak memerlukan Visual Studio untuk dijalankan.

## File yang biasa diedit

| File / proyek | Fungsi |
| --- | --- |
| Etika.Desktop.sln | Solution Visual Studio |
| src/Etika.Desktop/MainWindow.xaml | Tata letak login dan dokumen |
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
