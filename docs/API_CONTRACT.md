# Kontrak API Fase 1

Server dikonfigurasi sebagai root aplikasi, misalnya https://etika.kukarkab.com atau root staging. Klien menambahkan /api/v1/. Root dengan subdirektori dan URL yang sudah berakhiran /api/v1 didukung.

| Metode | Endpoint | Fungsi |
| --- | --- | --- |
| GET | auth/challenge | data.key dan data.img untuk CAPTCHA |
| POST | auth/login | Login; data.token, data.expires_at, data.user |
| GET | me | Identitas pengguna |
| GET | context | data.tahun_aktif dan data.tahun |
| GET | dokumen?tahun=2026&page=1&per_page=20&q= | Daftar terpaginasikan |
| GET | dokumen/{id} | Rincian dan data.lampiran |
| GET | lampiran/{id}/download | application/pdf dari server |
| POST | auth/logout | Pencabutan token aktif; HTTP 204 |

Login JSON: email, password, captcha_key, captcha, device_name. Kredensial tidak dikirim pada query URL. Permintaan sesudah login menyertakan Authorization: Bearer, dengan sesi yang dibatasi masa berlakunya.

Daftar memakai bentuk { data: [...], meta: { current_page, last_page, total, per_page } }. Rincian memakai { data: {...} }.

Field dokumen: id (string), tahun (integer), jenis, tanggal, unit_kode, unit_nama, unit_singkatan, subkegiatan_kode, subkegiatan_nama, pagu_ppas (string), pagu_rka (string), status, status_verif, status_label, updated_at. Rincian menambahkan keterangan dan lampiran; setiap lampiran berisi id (string), nama, size.

Kode kesalahan yang ditangani: 401 sesi berakhir, 403 akun/hak akses, 404 data tidak tersedia atau API belum aktif, 409 konteks tahun belum tersedia, 413 batas ukuran, 415 PDF tidak valid, 422 validasi, 429 frekuensi permintaan. Redirect HTTP tidak diikuti agar token tetap menuju API yang dikonfigurasi.

Pembacaan PDF dibatasi 25 MiB. File harus bertipe application/pdf dan memiliki signature %PDF-. PDF terlindungi kata sandi belum didukung viewer pada fase ini.

Sumber kontrak backend: https://github.com/novay/etika.kukarkab.com/pull/1
