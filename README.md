# ETIKA Desktop — Fase 1

Aplikasi Windows native C# + WinUI 3 yang membaca data ETIKA melalui API pada backend Laravel existing. Source desktop dipisahkan dari proyek web; database dan penyimpanan dokumen tetap berada pada server ETIKA.

Status: implementasi alur baca awal. Klien API dan API Laravel sudah diuji. Build XAML, paket Windows, dan pengujian UI pada Windows masih perlu dijalankan.

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
