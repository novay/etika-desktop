# Fase 2 — Stabilisasi UI dan integrasi klien

Versi source: 0.2.1. API Laravel dan skema database existing tidak diubah pada fase ini.

## Implementasi

- Tombol Coba lagi untuk kegagalan koneksi/server serta tombol Batalkan selama permintaan aktif. Tidak ada pengulangan login otomatis di latar belakang.
- Sesi tidak sah menghapus tampilan akun/dokumen, menutup PDF, dan memuat CAPTCHA baru. Kegagalan sementara saat memulihkan sesi dapat dicoba kembali.
- Kegagalan penyimpanan Credential Locker ditampilkan sebagai peringatan setelah login; pengguna tetap dapat bekerja selama sesi aplikasi berjalan.
- Tema dan alamat server disimpan bersama dengan pembaruan file atomik. Format pengaturan lama tetap dapat dibaca.
- Pengaturan dan Keluar memakai label/icon yang sesuai. Daftar kosong, pemuatan gagal, dan pemuatan aktif ditampilkan terpisah.
- Muat ulang menyegarkan pilihan tahun serta dokumen. Halaman yang hilang setelah perubahan data kembali ke halaman yang masih tersedia.
- Viewer PDF menampilkan nama lampiran, navigasi halaman, nomor halaman, zoom, dan penanganan kegagalan. Pekerjaan yang masih aktif dibatalkan saat viewer ditutup; stream dibersihkan sesudah pekerjaan selesai. Ukuran raster dibatasi pada kedua dimensinya.
- Profil ETIKA Local API mengarahkan build Debug ke http://127.0.0.1:8000. Profil ETIKA UI Preview tetap tersedia untuk desain tanpa API.

## Pengujian otomatis

Pengujian klien mencakup pembatasan respons PDF yang dikirim tanpa Content-Length, pembatalan permintaan, penyimpanan tema/server, pengaturan rusak, pembatasan raster PDF, dan pesan gangguan server.

scripts/Test-ApiIntegration.ps1 menjalankan aplikasi Debug dengan server HTTP sintetis yang hanya mendengarkan pada 127.0.0.1. Data PDF, akun, CAPTCHA, serta dokumen adalah fixture pengujian. Pemeriksaan ini menguji klien Windows dan protokol HTTP, bukan implementasi Laravel atau database produksi.

Cakupan target: login salah/benar, CAPTCHA baru, peran, pencarian, paginasi, tahun, data kosong, perubahan server yang dibaca ulang, pembatalan, retry, tema, sesi, rendering PDF dua halaman, zoom, PDF rusak/terkunci/hilang/berukuran besar, serta pembersihan saat logout. Eksekusi terakhir lulus seluruh cakupan tersebut, termasuk pemulihan Credential Locker setelah restart. Hasil lengkap dan batas pengujian dicatat di VALIDATION.md.

Variabel ETIKA_DEV_SERVER (hanya alamat loopback), ETIKA_TEST_DATA_DIR, dan ETIKA_UI_PREVIEW dibaca pada build Debug. Build Release mengabaikannya. Fixture tidak ikut dipublikasikan dalam folder aplikasi.

## Penerimaan pada lingkungan pengguna — masih perlu dilakukan

| Pemeriksaan | Kebutuhan | Status |
| --- | --- | --- |
| F5 + XAML Hot Reload | Visual Studio pada Windows pengguna | Menunggu uji perangkat |
| Akun Admin, Eksekutif, SKPD, Berkas, TAPD | Akun uji sah pada backend tujuan | Menunggu UAT |
| Kepemilikan/unit/tim dan akses PDF | Laravel + MySQL + storage tujuan | Menunggu UAT |
| Perubahan dibuat melalui ETIKA web lalu dimuat ulang di desktop | Dua klien pada backend yang sama | Menunggu UAT |
| Credential Locker pada perangkat pengguna | Login, tutup aplikasi, buka kembali, logout | Menunggu UAT |
| Skala layar 100%, 125%, 150%, keyboard, jendela sempit/lebar | Perangkat Windows tujuan | Menunggu UAT |
| Windows ARM64 bila digunakan | Windows ARM dan toolchain sesuai | Menunggu uji perangkat |

Implementasi fase ini dapat ditinjau dan diuji. Penutupan penerimaan Fase 2 memerlukan hasil UAT di atas, terutama sebelum menambahkan operasi pengajuan/tulis pada Fase 3. Daftar langkah rinci tersedia pada WINDOWS_ACCEPTANCE.md, dengan matriks pencatatan hasil pada PHASE2_UAT.md.
