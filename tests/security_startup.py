"""Integration checks: real application processes, disposable SQLite only. Requires Python 3 and dotnet-ef 9.0.5."""
from contextlib import closing
import html
import http.cookiejar
import re
import json
import os
from pathlib import Path
import secrets
import socket
import sqlite3
import subprocess
import tempfile
import time
import urllib.request
import urllib.parse

ROOT = Path(__file__).resolve().parents[1]
DLL = ROOT / 'bin/Debug/net9.0/DystenSoftwateManagment.dll'
SECRET = secrets.token_urlsafe(32) + 'aA1!'
OTHER_SECRET = secrets.token_urlsafe(32) + 'bB2!'


def require(condition, message):
    if not condition:
        raise AssertionError(message)


def run_app(folder, db, enabled=False, username='', password='', error=None, login_password=None):
    with socket.socket() as sock:
        sock.bind(('127.0.0.1', 0))
        port = sock.getsockname()[1]
    env = {k: v for k, v in os.environ.items()
           if not k.lower().startswith(('rootuser', 'connectionstrings', 'aspnetcore_', 'dotnet_'))}
    env.update(ASPNETCORE_ENVIRONMENT='Production', DOTNET_ENVIRONMENT='Production',
               RootUser__Enabled=str(enabled).lower(), RootUser__UserName=username,
               RootUser__Password=password, ConnectionStrings__DefaultConnection=f'Data Source={db}')
    log = folder / 'startup.log'
    with log.open('w', encoding='utf-8') as output:
        process = subprocess.Popen(['dotnet', str(DLL), '--contentRoot', str(folder),
                                    '--urls', f'http://127.0.0.1:{port}'],
                                   cwd=folder, env=env, stdout=output, stderr=output)
        ready = False
        try:
            for _ in range(150):
                if process.poll() is not None:
                    break
                if 'Now listening on:' in log.read_text(encoding='utf-8', errors='replace'):
                    ready = True
                    break
                time.sleep(0.1)
            if error is None:
                require(ready, 'Application did not start (captured log is kept private).')
                with urllib.request.urlopen(f'http://127.0.0.1:{port}/Account/Login', timeout=10) as response:
                    require(response.status == 200, 'Login page unavailable')
                if login_password:
                    cookies = http.cookiejar.CookieJar()
                    opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(cookies))
                    url = f'http://127.0.0.1:{port}/Account/Login'
                    with opener.open(url) as response:
                        page = response.read().decode('utf-8')
                    token = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"\s]+)"', page)
                    require(token is not None, 'Antiforgery token missing')
                    payload = urllib.parse.urlencode({'Email': username, 'Password': login_password,
                                                      '__RequestVerificationToken': html.unescape(token.group(1))}).encode()
                    with opener.open(url, data=payload) as response:
                        response.read()
                    require(any(cookie.name == '.AspNetCore.Identity.Application' for cookie in cookies),
                            'Identity login did not issue authentication cookie')
            else:
                require(not ready and process.poll() is not None, 'Incomplete/invalid bootstrap must fail startup')
        finally:
            if process.poll() is None:
                process.terminate()
            process.wait(timeout=15)
    content = log.read_text(encoding='utf-8', errors='replace')
    for value in (SECRET, OTHER_SECRET, password):
        if value:
            require(value not in content, 'Secret detected in captured log')
    if error:
        require(error in content, 'Expected safe configuration error was not reported')


def main():
    with tempfile.TemporaryDirectory(prefix='dysten-security-') as temporary:
        folder = Path(temporary)
        # No source configuration or production database is copied to the sandbox.
        (folder / 'appsettings.json').write_text(json.dumps({'RootUser': {'Enabled': False}}))
        db = folder / 'appDataBase.db'
        env = dict(os.environ, ASPNETCORE_ENVIRONMENT='Production', DOTNET_ENVIRONMENT='Production',
                   RootUser__Enabled='false', RootUser__UserName='', RootUser__Password='',
                   ConnectionStrings__DefaultConnection=f'Data Source={db}')
        migration = subprocess.run(['dotnet', 'ef', 'database', 'update', '--no-build',
                                    '--project', str(ROOT / 'DystenSoftwateManagment.csproj'),
                                    '--connection', f'Data Source={db}'], cwd=folder,
                                   env=env, capture_output=True, text=True)
        require(migration.returncode == 0, 'Migration of disposable database failed')
        run_app(folder, db)
        with closing(sqlite3.connect(db)) as conn:
            require(conn.execute('SELECT COUNT(*) FROM AspNetUsers').fetchone()[0] == 0, 'Disabled bootstrap created a user')
            require({row[0] for row in conn.execute('SELECT Name FROM AspNetRoles')} == {'Admin', 'Developer', 'User'}, 'Roles missing')
        print('PASS disabled bootstrap and HTTP login')
        run_app(folder, db, True, 'portfolio-test', '', 'RootUser:UserName and RootUser:Password')
        run_app(folder, db, True, '', SECRET, 'RootUser:UserName and RootUser:Password')
        print('PASS incomplete configuration fails safely')
        run_app(folder, db, True, 'portfolio-test', secrets.token_hex(3), 'PasswordTooShort')
        with closing(sqlite3.connect(db)) as conn:
            require(conn.execute('SELECT COUNT(*) FROM AspNetUsers').fetchone()[0] == 0, 'Weak password created a user')
        print('PASS password policy rejects weak password')
        run_app(folder, db, True, 'portfolio-test', SECRET, login_password=SECRET)
        with closing(sqlite3.connect(db)) as conn:
            before = conn.execute('SELECT Id, PasswordHash, SecurityStamp FROM AspNetUsers').fetchall()
            roles = {row[0] for row in conn.execute('SELECT r.Name FROM AspNetRoles r JOIN AspNetUserRoles ur ON r.Id=ur.RoleId')}
            require(len(before) == 1 and roles == {'Admin', 'Developer'}, 'Administrator or roles not created')
        print('PASS administrator created on migrated empty database')
        run_app(folder, db, True, 'portfolio-test', OTHER_SECRET, login_password=SECRET)
        run_app(folder, db, False, '', '')
        with closing(sqlite3.connect(db)) as conn:
            require(before == conn.execute('SELECT Id, PasswordHash, SecurityStamp FROM AspNetUsers').fetchall(), 'Existing credentials were modified')
            conn.execute("DELETE FROM AspNetUserRoles WHERE RoleId IN (SELECT Id FROM AspNetRoles WHERE Name='Admin')")
            conn.commit()
        print('PASS restart preserves password hash and security stamp')
        run_app(folder, db, True, 'portfolio-test', SECRET, 'existing account')
        with closing(sqlite3.connect(db)) as conn:
            require(conn.execute("SELECT COUNT(*) FROM AspNetUserRoles ur JOIN AspNetRoles r ON r.Id=ur.RoleId WHERE r.Name='Admin'").fetchone()[0] == 0, 'Existing user was promoted')
        print('PASS existing ordinary account is not promoted; no passwords in logs')


if __name__ == '__main__':
    main()
