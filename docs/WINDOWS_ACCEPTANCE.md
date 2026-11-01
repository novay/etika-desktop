# Validasi Windows dan Staging

Gunakan aplikasi Laravel staging dan salinan database development. Tahap ini memakai akun uji yang sah; source tidak menyertakan kredensial.

1. Jalankan workflow Windows atau publish lokal; pastikan executable dapat dibuka dari seluruh folder hasil publish.
2. Pastikan tampilan login, CAPTCHA, pesan validasi, keyboard, tema terang/gelap, serta skala layar 100%, 125%, dan 150% terbaca.
3. Coba login benar/salah; pastikan CAPTCHA diperbarui setelah kegagalan dan akun nonaktif ditolak.
4. Periksa lima peran terhadap cakupan API yang didokumentasikan. Berkas dan TAPD hanya mendapat antrean stage sesuai tim; SKPD hanya pengajuannya sendiri.
5. Pilih tahun aktif dan tahun lain yang berisi dokumen; coba pencarian, paginasi, data kosong, dan detail.
6. Buka PDF sah dari storage staging; periksa beberapa halaman, zoom, dan scroll. Berkas hilang, palsu, besar, dan PDF berpassword harus mendapat hasil yang dapat dipahami.
7. Muat ulang setelah perubahan data melalui web; pastikan desktop membaca nilai pusat terbaru.
8. Nonaktifkan akun atau cabut token ketika sesi desktop terbuka; permintaan berikutnya harus ditolak.
9. Keluar; pastikan jendela PDF tertutup dan daftar/detail hilang. Saat offline, pastikan peringatan pencabutan token server ditampilkan.
10. Uji opsi Ingat sesi, tutup/buka aplikasi, kedaluwarsa token, serta perpindahan server. Token harus tersimpan melalui Credential Locker.
11. Uji login web, monitoring existing, dan akses lampiran web sesudah perubahan backend.
12. Setelah seluruh langkah lolos, catat hasil, versi Windows, arsitektur, commit backend/desktop, dan jalur deployment yang diuji.

Tahap berikutnya sesudah penerimaan alur baca adalah pengajuan dan unggahan, diikuti catatan pemeriksaan serta anotasi PDF. Aturan tulis dan status akan berada pada backend bersama.
