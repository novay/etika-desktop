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

Untuk workspace v0.2.0:

- Bandingkan menu Admin, Eksekutif, SKPD, Berkas, TAPD, dan akun dengan beberapa peran terhadap docs/UI_WORKSPACE.md. Uji filter tahap bersama tahun, pencarian, dan paginasi memakai akun staging.
- Periksa daftar/rincian berdampingan pada lebar besar, serta tombol Kembali ke daftar pada jendela sempit. Periksa sidebar saat melebar, menyusut, dan menggunakan keyboard.
- Buka Akun saya dan Pengaturan, lalu periksa tema mengikuti Windows, terang, dan gelap.
- Dengan profil Debug ETIKA UI Preview, ganti peran tanpa login. Ubah padding dan teks pada XAML saat F5 berjalan untuk memverifikasi Hot Reload pada instalasi IDE yang dipakai.
- Jalankan profil normal kembali untuk login dan PDF sungguhan. Mode pratinjau memakai data sintetis dan tidak menggantikan pengujian otorisasi di server.

Tahap berikutnya sesudah penerimaan alur baca adalah pengajuan dan unggahan, diikuti catatan pemeriksaan serta anotasi PDF. Aturan tulis dan status akan berada pada backend bersama.


Fase 2 v0.2.1 sudah memiliki pemeriksaan klien dan fixture Windows otomatis. Pengujian perangkat serta backend sebenarnya tetap perlu dicatat pada [PHASE2_UAT.md](PHASE2_UAT.md); ringkasan hasil otomatis ada di [VALIDATION.md](VALIDATION.md).
