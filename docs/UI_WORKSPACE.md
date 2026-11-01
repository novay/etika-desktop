# ETIKA Desktop — Ruang kerja WinUI 3

Antarmuka memakai title bar native, Mica, navigasi samping adaptif, beranda, kartu dokumen, rincian lampiran, profil akun, dan pilihan tema. Pada lebar konten di bawah 960 DIP, daftar serta rincian ditampilkan bergantian dengan tombol kembali. Pada jendela yang lebih lebar, keduanya tampil berdampingan.

## Menu sesuai peran

Semua akun yang didukung memiliki Beranda, Akun saya, Pengaturan, dan Keluar.

| Peran | Menu dokumen |
| --- | --- |
| Admin | Semua dokumen, Pemeriksaan berkas, Verifikasi TAPD |
| Eksekutif | Monitoring dokumen |
| SKPD | Dokumen saya |
| Berkas | Pemeriksaan berkas |
| TAPD | Verifikasi TAPD |
| SKPD + TAPD | Dokumen sesuai akses, Verifikasi TAPD |

Peran dipisahkan memakai koma atau tanda pipa, tanpa membedakan huruf besar/kecil. Peran tidak dikenal tidak memperoleh menu dokumen. Bila akun memiliki beberapa peran terbatas, halaman gabungan mengikuti union cakupan yang diberikan API; UI tidak menjanjikan filter kepemilikan terpisah yang belum tersedia pada API. Admin/Eksekutif menggunakan cakupan pemantauan lintas unit yang sudah didukung backend.

Pemeriksaan berkas mengirim status=1; Verifikasi TAPD mengirim status=3. Filter dikirim bersama pencarian, tahun, dan paginasi ke endpoint existing. Menu ini adalah tampilan dokumen, bukan aksi persetujuan atau perubahan status. Untuk Admin, filter tahap juga dapat memuat dokumen selesai bila status tahapnya masih sama; pemeriksa Berkas/TAPD mengikuti batas antrean yang diterapkan backend.

Penentuan menu tidak menggantikan otorisasi. API tetap memeriksa akun, tim, unit, kepemilikan, dan akses setiap detail serta unduhan. Pengajuan, anotasi, persetujuan, dan manajemen pengguna belum ditambahkan karena kontrak API fase 1 masih berfokus pada pembacaan dokumen.

Jumlah di beranda berasal dari meta.total untuk tahun dan cakupan halaman utama peran. Jumlah tersebut bukan total lintas tahun atau statistik seluruh database. Beranda dimuat ulang saat dibuka dan saat tahun diubah.

## Mengubah desain

- MainWindow.xaml: layout, template, teks, dan komponen antarmuka.
- Styles/Workspace.xaml: style bersama yang mengikuti tema.
- MainWindow.xaml.cs: navigasi dan interaksi.
- Etika.Core/WorkspaceNavigation.cs: label, deskripsi, dan filter menu peran.
- PreviewData.cs: data sintetis khusus Debug.

Alur F5 + XAML Hot Reload dan pratinjau peran dijelaskan pada [DEVELOPMENT_WINDOWS.md](DEVELOPMENT_WINDOWS.md).
