"""Loopback-only synthetic API for Windows client tests; not a Laravel/DB substitute."""
import argparse, base64, json, math, threading, time, uuid
from datetime import datetime, timedelta, timezone
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import urlparse, parse_qs

FIXTURES = json.loads(Path(__file__).with_name('documents.json').read_text())
STATE = {'mode': 'normal', 'empty': False, 'renamed': False, 'tokens': {}, 'logins': 0, 'challenges': 0, 'last_year': 2026, 'last_search': '', 'last_page': 1, 'last_status': None}
LOCK = threading.RLock()
ROLES = {'admin': 'Admin', 'eksekutif': 'Eksekutif', 'skpd': 'SKPD', 'berkas': 'Berkas', 'tapd': 'TAPD', 'multi': 'SKPD|TAPD'}

class Handler(BaseHTTPRequestHandler):
    protocol_version = 'HTTP/1.1'
    def log_message(self, *args): pass
    def respond(self, code, data, content_type='application/json', length=None):
        payload = json.dumps(data).encode() if content_type == 'application/json' else data
        try:
            self.send_response(code)
            self.send_header('Content-Type', content_type)
            self.send_header('Content-Length', str(len(payload) if length is None else length))
            self.send_header('Connection', 'close')
            self.end_headers(); self.wfile.write(payload)
        except (BrokenPipeError, ConnectionResetError): pass
        self.close_connection = True
    def do_POST(self):
        try: data = json.loads(self.rfile.read(int(self.headers.get('Content-Length', '0'))) or b'{}')
        except ValueError: return self.respond(400, {'message': 'Invalid JSON'})
        if self.path == '/__control':
            with LOCK:
                for key in ('mode', 'empty', 'renamed'):
                    if key in data: STATE[key] = data[key]
                if data.get('expire'): STATE['tokens'].clear()
            return self.respond(200, {'ok': True})
        if self.path == '/api/v1/auth/login':
            if data.get('captcha') != '12345' or data.get('captcha_key') != 'fixture-key':
                return self.respond(422, {'message': 'Kode verifikasi tidak sesuai.'})
            if data.get('password') != 'test-password':
                return self.respond(422, {'message': 'Email atau kata sandi tidak sesuai.'})
            role = ROLES.get(data.get('email', '').split('@')[0])
            if role is None: return self.respond(403, {'message': 'Peran akun belum didukung.'})
            token = uuid.uuid4().hex
            with LOCK:
                STATE['tokens'][token] = role; STATE['logins'] += 1
            return self.respond(200, {'data': {'token': token, 'expires_at': (datetime.now(timezone.utc) + timedelta(hours=1)).isoformat(), 'user': self.user(role)}})
        if self.path == '/api/v1/auth/logout':
            with LOCK: STATE['tokens'].pop(self.headers.get('Authorization', '').removeprefix('Bearer '), None)
            return self.respond(204, b'', 'application/octet-stream')
        self.respond(404, {'message': 'Not found'})
    def user(self, role): return {'id': 'fixture-user', 'name': 'Pengguna Uji', 'email': 'user@etika.test', 'level': role}
    def documents(self, role, year):
        result = []
        for i in range(1, 26):
            stage = (1, 3, 2)[(i-1) % 3]
            if role == 'Berkas' and stage != 1 or role == 'TAPD' and stage != 3: continue
            result.append({'id': str(i), 'tahun': year, 'jenis': 'Belanja', 'unit_kode': 'UNIT-UJI', 'unit_nama': 'Unit Uji',
                'unit_singkatan': 'UNIT UJI', 'subkegiatan_kode': f'2.19.01.{i:03}',
                'subkegiatan_nama': 'Perubahan dari web (uji)' if i == 1 and STATE['renamed'] else f'Dokumen uji {i:02}',
                'pagu_rka': str(1000000*i), 'pagu_ppas': str(1000000*i), 'status': stage, 'status_verif': 0,
                'status_label': {1:'Pemeriksaan berkas',2:'Revisi',3:'Verifikasi TAPD'}[stage]})
        return result
    def do_GET(self):
        parsed = urlparse(self.path); path = parsed.path; query = parse_qs(parsed.query)
        if path == '/__stats':
            return self.respond(200, {k: STATE[k] for k in ('logins','challenges','mode','last_year','last_search','last_page','last_status')})
        if path == '/api/v1/auth/challenge':
            with LOCK: STATE['challenges'] += 1
            return self.respond(200, {'data': {'key': 'fixture-key', 'img': FIXTURES['captcha']}})
        token = self.headers.get('Authorization', '').removeprefix('Bearer ')
        role = STATE['tokens'].get(token)
        if not role: return self.respond(401, {'message': 'Sesi berakhir. Silakan masuk kembali.'})
        if STATE['mode'] == 'inactive': return self.respond(403, {'message': 'Akun Anda dinonaktifkan.'})
        if path == '/api/v1/me': return self.respond(200, {'data': self.user(role)})
        if path == '/api/v1/context': return self.respond(200, {'data': {'tahun_aktif': 2026, 'tahun': [{'tahun': 2026,'aktif':True,'perubahan':False},{'tahun':2025,'aktif':False,'perubahan':False}]}})
        if path == '/api/v1/dokumen':
            if STATE['mode'] == 'failure': return self.respond(503, {'message': 'SQLSTATE fixture diagnostic must never reach UI'})
            if STATE['mode'] == 'slow': time.sleep(8)
            year = int(query.get('tahun', ['2026'])[0]); page = int(query.get('page', ['1'])[0])
            STATE.update(last_year=year, last_page=page, last_search=query.get('q',[''])[0], last_status=query.get('status',[None])[0])
            documents = [] if STATE['empty'] else self.documents(role, year)
            if query.get('status'): documents = [d for d in documents if d['status'] == int(query['status'][0])]
            term = query.get('q', [''])[0].casefold()
            documents = [d for d in documents if term in d['subkegiatan_nama'].casefold() or term in d['subkegiatan_kode']]
            return self.respond(200, {'data': documents[(page-1)*20:page*20], 'meta': {'current_page':page,'last_page':max(1,math.ceil(len(documents)/20)),'total':len(documents),'per_page':20}})
        if path.startswith('/api/v1/dokumen/'):
            identifier = path.split('/')[-1]
            doc = next((d for d in self.documents(role, STATE['last_year']) if d['id'] == identifier), None)
            if doc is None: return self.respond(404, {'message':'Dokumen tidak ditemukan.'})
            names = [('valid','PDF uji dua halaman'),('broken','PDF rusak'),('locked','PDF terkunci'),('missing','PDF hilang'),('html','Respons bukan PDF'),('large','PDF terlalu besar')]
            doc['lampiran'] = [{'id': key,'nama':name,'size':0} for key,name in names]
            doc['keterangan'] = 'Data sintetis untuk pengujian aplikasi Windows.'
            return self.respond(200, {'data':doc})
        if path.startswith('/api/v1/lampiran/'):
            key = path.split('/')[-2]
            if key == 'missing': return self.respond(404, {'message':'Berkas belum tersedia.'})
            if key == 'html': return self.respond(200, b'<html>Invalid file</html>', 'text/html')
            if key == 'large': return self.respond(200, b'', 'application/pdf', length=25*1024*1024+1)
            payload = b'%PDF-broken' if key == 'broken' else base64.b64decode(FIXTURES['locked' if key == 'locked' else 'pdf'])
            return self.respond(200, payload, 'application/pdf')
        self.respond(404, {'message':'Not found'})

if __name__ == '__main__':
    parser=argparse.ArgumentParser(); parser.add_argument('--ready-file',required=True);args=parser.parse_args()
    server=ThreadingHTTPServer(('127.0.0.1',0),Handler)
    Path(args.ready_file).write_text(json.dumps({'url':f'http://127.0.0.1:{server.server_port}'}))
    server.serve_forever()
