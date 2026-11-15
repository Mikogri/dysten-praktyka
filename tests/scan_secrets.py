"""Heuristic scan. Prints locations only, never matching secret values. Not a proof of absence."""
from pathlib import Path
import json
import re
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
PATTERNS = {
    'credential assignment': re.compile(rb'''(?i)(?<![A-Za-z0-9_])["']?(?:password|passwd|pwd|clientsecret|client_secret|api_key|apikey|access_token|refresh_token)["']?\s*[:=]\s*["']([^"'\r\n]+)["']'''),
    'connection credential': re.compile(rb"(?i)\b(?:password|pwd)\s*=\s*[^;\s\"']+(?:;|$)"),
    'private key': re.compile(rb'-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----'),
    'provider token': re.compile(rb'\b(?:gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{30,}|AKIA[A-Z0-9]{16}|sk-[A-Za-z0-9]{30,})\b'),
}


def git(*args):
    return subprocess.check_output(['git', '-C', str(ROOT), *args])


def known_exposed_passwords():
    values = set()
    baseline = ROOT
    if '--baseline' in sys.argv:
        baseline = Path(sys.argv[sys.argv.index('--baseline') + 1]).resolve()
    def baseline_git(*args):
        return subprocess.check_output(['git', '-C', str(baseline), *args])
    revisions = set(baseline_git('log', '--all', '--reflog', '--format=%H', '--', 'appsettings.json').decode().splitlines())
    for revision in revisions:
        result = subprocess.run(['git', '-C', str(baseline), 'show', revision + ':appsettings.json'], capture_output=True)
        if result.returncode != 0:
            continue
        try:
            password = json.loads(result.stdout.decode('utf-8-sig')).get('RootUser', {}).get('Password')
            if isinstance(password, str) and password:
                values.add(password.encode())
        except (ValueError, UnicodeError):
            continue
    return values


EXPOSED_PASSWORDS = None


def scan(label, data):
    if b'\0' in data[:8000]:
        return 0
    count = 0
    for password in EXPOSED_PASSWORDS or ():
        if password in data:
            print(f'{label}: review known exposed password')
            count += 1
    for kind, pattern in PATTERNS.items():
        for match in pattern.finditer(data):
            # Configuration API lookups and documentation placeholders are not assigned secrets.
            if kind == 'credential assignment' and (match.group(1).startswith((b'<', b'{')) or match.group(1) in (b'Password', b'password', b'RootUser:Password')):
                continue
            line = data.count(b'\n', 0, match.start()) + 1
            source_line = data.splitlines()[line - 1]
            if kind == 'credential assignment' and b'IsPassword ? "password" : "text"' in source_line:
                continue
            print(f'{label}:{line}: review {kind}')
            count += 1
    return count


def main():
    global EXPOSED_PASSWORDS
    EXPOSED_PASSWORDS = known_exposed_passwords()
    count = 0
    if '--history' in sys.argv:
        seen = set()
        for record in git('rev-list', '--objects', '--all', '--reflog').decode().splitlines():
            oid, _, path = record.partition(' ')
            if oid in seen:
                continue
            seen.add(oid)
            kind = git('cat-file', '-t', oid).decode().strip()
            if kind in ('blob', 'commit', 'tag'):
                count += scan(f'{oid[:12]} {path or kind}', git('cat-file', kind, oid))
        print(f'Inspected {len(seen)} reachable/reflog objects; review findings: {count}')
    else:
        paths = set(git('ls-files', '--cached', '--others', '--exclude-standard').decode().splitlines())
        for name in sorted(paths):
            path = ROOT / name
            if path.is_file():
                count += scan(name, path.read_bytes())
        print(f'Inspected {len(paths)} working-tree paths; review findings: {count}')
    return int(count > 0)


if __name__ == '__main__':
    sys.exit(main())
