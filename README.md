# ETIKA Desktop — Fase 1

Aplikasi Windows native C# + WinUI 3 yang membaca data ETIKA melalui API pada backend Laravel existing. Source desktop dipisahkan dari proyek web; database dan penyimpanan dokumen tetap berada pada server ETIKA.

Status v0.1.2: build Release x64, build solution Debug x64, 16 pengujian klien, dan uji startup untuk kedua konfigurasi berhasil pada runner Windows. Integrasi staging dan pengujian fitur interaktif masih diperlukan.

## Unduh aplikasi Windows x64

[Paket hasil build Windows x64](https://github.com/novay/etika-desktop/actions/runs/41299327177/artifacts/13081035183) — sekitar 86 MiB. [Log build dan pengujian](https://github.com/novay/etika-desktop/actions/runs/41299327177).

Ekstrak ZIP v0.1.2 ke folder baru, lalu jalankan Etika.Desktop.exe dari folder tersebut. Seluruh DLL dan file pendukung hasil publish harus tetap berada bersama executable. Ini paket portable hasil publish; installer dan penandatanganan belum disiapkan.

Aplikasi belum dapat login ke server produksi sebelum API desktop diterapkan dan diaktifkan. Gunakan alamat staging yang sudah memiliki API sesuai runbook backend.

## Fitur source yang tersedia

- Login akun ETIKA dengan CAPTCHA.
- Penyimpanan sesi di memori; opsi mengingat sesi memakai Windows Credential Locker.
- Pilihan tahun anggaran, pencarian, dan paginasi dokumen.
- Rincian dokumen dan daftar lampiran.
- Viewer PDF native: halaman sebelumnya/berikutnya, zoom, scroll horizontal/vertikal.
- Penanganan sesi kedaluwarsa, akun tidak diizinkan, koneksi gagal, data kosong, dan logout.
- Penutupan jendela PDF serta penghapusan data tampilan saat logout.

Belum mencakup pengajuan, anotasi, perubahan status, diskusi, berita acara, installer, dan pembaruan otomatis.

## Source dan backend

- src/Etika.Core: kontrak data dan klien HTTPS; dapat diuji pada macOS/Linux/Windows.
- src/Etika.Desktop: antarmuka WinUI, sesi Windows, dan viewer PDF.
- tests/Etika.Core.Tests: pengujian kontrak dan perilaku klien tanpa paket tes tambahan.
- .github/workflows/windows.yml: build Windows x64 dan unggah hasil publish sebagai artifact.

Backend API: https://github.com/novay/etika.kukarkab.com/pull/1

Source backend tetap berada di repository novay/etika.kukarkab.com. Sebelum login desktop dapat digunakan, API perlu diterapkan dan diaktifkan di development/staging sesuai runbook pada PR.

## Menjalankan pengujian klien

Pasang .NET SDK 10.0, kemudian dari direktori ini:

    dotnet run --project tests/Etika.Core.Tests

Hasil lokal: 15 pemeriksaan lulus. Lihat docs/VALIDATION.md untuk batas verifikasi.

## Membuka source di Visual Studio

Buka Etika.Desktop.sln menggunakan Visual Studio 2026 dengan workload WinUI application development. Pilih proyek Etika.Desktop sebagai startup project, konfigurasi Debug / x64, profil ETIKA Desktop (Unpackaged), lalu tekan F5. [Panduan IDE dan debugging](docs/DEVELOPMENT_WINDOWS.md).

## Build dan menjalankan di Windows

Gunakan Windows dengan Visual Studio yang mendukung .NET 10 dan pengembangan WinUI, atau toolchain Windows App SDK yang sesuai. Source meminimalkan versi Windows pada build 17763; target SDK adalah 26100.

    dotnet publish src/Etika.Desktop/Etika.Desktop.csproj -c Release -r win-x64 -p:Platform=x64 --self-contained true -o artifacts/Etika.Desktop

Jalankan artifacts/Etika.Desktop/Etika.Desktop.exe. Distribusikan seluruh folder hasil publish karena dependensi runtime ikut berada di dalamnya. Paket installer dan penandatanganan belum dibuat pada fase ini.

Di layar login, masukkan alamat server staging ETIKA dan pilih Ganti kode. Gunakan akun uji existing, isi CAPTCHA, lalu masuk. Pilih tahun yang berisi dokumen; tahun aktif pada server dapat berbeda dari tahun dokumen dalam dump.

HTTP hanya diterima untuk localhost/loopback pada pengembangan. Server lain menggunakan HTTPS dan pemeriksaan sertifikat bawaan platform.

## Repository dan build otomatis

Source desktop dikelola pada https://github.com/novay/etika-desktop. Push ke repository memicu workflow Windows Desktop. Artifact Etika.Desktop-win-x64 tersedia pada halaman run GitHub Actions setelah build berhasil.

Backend berada pada repository ETIKA web existing melalui PR yang ditautkan di atas. Build desktop dan deployment backend dikelola terpisah.

## Integrasi data

Aplikasi mengakses API Laravel, bukan koneksi SQL. Daftar/detail dan unduhan diperiksa hak aksesnya di server. Pembaruan web tampil setelah desktop memuat ulang data. Tidak ada database operasional kedua dan tidak ada dump produksi dalam paket ini.

Rincian kontrak terdapat di docs/API_CONTRACT.md. Validasi akhir Windows dan integrasi staging tercantum di docs/WINDOWS_ACCEPTANCE.md.

## Jika kode verifikasi tidak muncul

Desktop mengambil kode verifikasi dari GET /api/v1/auth/challenge. Server harus menyediakan API desktop pada [backend ETIKA](https://github.com/novay/etika.kukarkab.com/pull/1), dengan konfigurasi ETIKA_DESKTOP_API_ENABLED=true dan cache route/config yang sesuai. Menghubungkan repository desktop saja belum memasang endpoint ini pada server web.

HTTP 404 pada layanan challenge/login ditampilkan sebagai pesan bahwa layanan login desktop belum tersedia. Tombol Masuk baru aktif setelah gambar CAPTCHA dan key berhasil dimuat. Kredensial tidak dikirim bila kode verifikasi belum siap. Setelah backend siap, klik Ganti kode untuk mencoba kembali.
