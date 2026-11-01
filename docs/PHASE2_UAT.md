# Penerimaan Fase 2 pada perangkat dan backend tujuan

Status awal: belum diuji oleh pengguna. Catat hasil aktual; hasil fixture/CI tidak mengisi tabel ini secara otomatis.

## Lingkungan

- Commit desktop:
- Commit backend:
- Versi Windows dan arsitektur:
- Versi Visual Studio:
- Skala layar:
- Backend staging yang digunakan:
- Tanggal pemeriksaan:

## Development

1. Clone/pull main pada Windows, buka Etika.Desktop.sln, lalu jalankan scripts/Check-DevEnvironment.ps1 dari PowerShell.
2. Pilih Debug dan platform sesuai perangkat. Jalankan profil ETIKA UI Preview dengan F5.
3. Pada beranda, ubah teks statis RUANG KERJA ETIKA di MainWindow.xaml. Simpan dan pastikan jendela yang sama memperbarui teks. Kembalikan perubahan uji setelah selesai.
4. Uji tema terang/gelap, navigasi keyboard, jendela sempit/lebar, dan skala 100%, 125%, 150%.
5. Untuk API sebenarnya, hentikan debugger lalu pilih profil normal. Isi Server ETIKA sesuai backend tujuan. Profil Local API ditujukan untuk Laravel pada PC Windows yang sama.

## Matriks penerimaan

| Kasus | Hasil yang diharapkan | Status / catatan |
| --- | --- | --- |
| Login benar/salah | Validasi jelas, CAPTCHA baru setelah gagal | Belum diuji |
| Admin | Semua dokumen pada tahun terpilih; filter Berkas/TAPD sesuai tahap | Belum diuji |
| Eksekutif | Monitoring sesuai kewenangan backend | Belum diuji |
| SKPD | Pengajuan milik akun dan unitnya; detail/unduhan di luar cakupan ditolak | Belum diuji |
| Berkas | Antrean tahap 1 sesuai tim; di luar tim ditolak | Belum diuji |
| TAPD | Antrean tahap 3 sesuai tim; di luar tim ditolak | Belum diuji |
| Akun beberapa peran, bila digunakan | Menu dan cakupan gabungan sesuai aturan backend | Belum diuji |
| Tahun, pencarian, halaman, hasil kosong | Daftar dan jumlah mengikuti pilihan | Belum diuji |
| Perubahan dari web | Ubah dokumen uji di web; muat ulang lalu pilih kembali di desktop, nilai terbaru tampil | Belum diuji |
| Lampiran dari storage tujuan | PDF sah terbuka; halaman/zoom benar; berkas rusak/terkunci/hilang mendapat pesan jelas | Belum diuji |
| Gangguan koneksi | Pesan jelas, coba lagi/batal bekerja, sesi tidak hilang akibat kegagalan sementara | Belum diuji |
| Ingat sesi | Tutup aplikasi lalu buka kembali; server dan akun tetap sesuai; token tidak ditulis ke settings.json | Belum diuji |
| Logout / sesi dicabut / akun nonaktif | Data serta viewer dibersihkan; login dan CAPTCHA tersedia kembali | Belum diuji |
| Tema dan server | Preferensi bertahan sesudah aplikasi dibuka kembali | Belum diuji |
| Hot Reload, DPI, keyboard, resize | Hasil sesuai langkah development di atas | Belum diuji |
| Regresi web | Login, monitoring, dan lampiran web tetap bekerja | Belum diuji |

Gunakan akun/data uji staging untuk perubahan status akun dan perubahan dokumen. Jangan memasukkan kata sandi, token, atau data dokumen produksi ke catatan repository. Jika ada kegagalan, catat langkah, pesan, versi, dan screenshot yang sesuai.

Penerimaan Fase 2 ditutup setelah kasus yang berlaku lulus pada lingkungan tujuan. Langkah ini terpisah dari lulusnya build dan fixture otomatis.
