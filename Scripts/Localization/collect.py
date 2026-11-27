"""Collect Unity text defaults, prefab overrides and authored copy. Run from the repo root."""
import argparse
import csv
import hashlib
import re
import subprocess
import tempfile
from pathlib import Path

FIELD = re.compile(r'^  (m_text|m_Text): *(.*)$')
OVERRIDE = re.compile(r'^\s+propertyPath: (m_text|m_Text)$')
BLOCK = re.compile(r'^--- !u!(\d+) &(-?\d+)')
HANGUL = re.compile(r'[가-힣]')
ESC = re.compile(r'\\(?:u([0-9a-fA-F]{4})|U([0-9a-fA-F]{8})|x([0-9a-fA-F]{2})|(.))', re.S)
SIMPLE = {'n': '\n', 'r': '\r', 't': '\t', '"': '"', '\\': '\\', '/': '/', '0': '\0', ' ': ' ', 'N': '\x85', '_': '\xa0'}

def unescape(value):
    def char(m):
        code = m[1] or m[2] or m[3]
        return chr(int(code, 16)) if code else SIMPLE.get(m[4], m[0])
    return ESC.sub(char, value)

def scalar(lines, i, raw):
    if raw.startswith('"'):
        while not re.search(r'(?<!\\)(?:\\\\)*"$', raw[1:]) and i + 1 < len(lines):
            i += 1
            raw = raw[:-1] + lines[i].strip() if raw.endswith('\\') else raw + ' ' + lines[i].strip()
        return unescape(raw[1:-1])
    if raw.startswith("'"):
        while not raw.endswith("'") and i + 1 < len(lines):
            i += 1
            raw += ' ' + lines[i].strip()
        return raw[1:-1].replace("''", "'")
    return raw

def rg_files(pattern, globs, root):
    args = ['rg', '--no-ignore', '--crlf', '-l', pattern, root]
    for glob in globs:
        args.extend(['-g', glob])
    proc = subprocess.run(args, capture_output=True)
    if proc.returncode not in (0, 1):
        raise RuntimeError(proc.stderr.decode(errors='replace'))
    return sorted(Path(s) for s in proc.stdout.decode('utf-8').splitlines())

def key_for(value):
    return 'display.' + hashlib.sha256(value.encode('utf-8')).hexdigest()[:12]

def collect():
    assets = rg_files(r'^  m_(Text|text):|propertyPath: m_(Text|text)$', ['*.unity','*.prefab'], 'Assets')
    for path in assets:
        lines = path.read_text(encoding='utf-8-sig').splitlines()
        component = ''
        for i, line in enumerate(lines):
            header = BLOCK.match(line)
            if header:
                component = header[2]
            field = FIELD.match(line)
            override = OVERRIDE.match(line)
            if field:
                name, raw = field.groups()
            elif override and i + 1 < len(lines) and lines[i+1].lstrip().startswith('value:'):
                name = override[1] + ' override'
                i += 1
                raw = lines[i].lstrip()[6:].lstrip()
            else:
                continue
            value = scalar(lines, i, raw)
            yield ('component', path.as_posix(), component, name, key_for(value) if value.strip() else '', value)
    folder = Path('Assets/Resources/Settings/WaitingRoom')
    # Inspector-authored copy can supersede the defaults in the two text files.
    for path in rg_files(r'\\u[0-9A-Fa-f]{4}|[가-힣]|@loc:', ['*.asset', '*.prefab', '*.unity', '!**/Fonts/**'], 'Assets'):
        if '_ThirdParty' in path.parts and path.suffix == '.asset':
            continue
        lines = path.read_text(encoding='utf-8-sig').splitlines()
        row_id = ''
        for i, line in enumerate(lines):
            field = re.match(r'^\s+(?:- )?([a-zA-Z][\w]*):\s*(.+)$', line)
            if not field:
                continue
            name, raw = field.groups()
            if name == 'id':
                row_id = raw.strip()
            shop_copy = path.name == 'ShopUiTexts.asset' and not name.startswith('m_') and not name.endswith('Icon')
            if not ((shop_copy or re.search(r'(?i)text|label|title|body|caption|message|notice|desc|prompt|hint|badge|guide|displayname|story|subtitle|tooltip', name))
                    and name not in ('m_Text','m_text','m_Name','m_EditorClassIdentifier')):
                continue
            if not ('\\u' in raw or HANGUL.search(raw) or '@loc:' in raw or (shop_copy and '{' in raw)):
                continue
            value = scalar(lines, i, raw)
            if not HANGUL.search(value) and not value.startswith('@loc:') and not (shop_copy and '{' in value):
                continue
            key = key_for(value)
            if path.name == 'WaitingRoomHud.asset' and row_id:
                if name == 'text':
                    key = 'room.' + row_id
                elif name in ('title','body'):
                    key = 'tutorial.' + row_id.lower() + '.' + name
            yield ('inspector',path.as_posix(),str(i+1),name,key,value)
    for name in ['WaitingRoomTexts','WaitingRoomTutorialTexts']:
        path = folder / (name + '.txt')
        for i, line in enumerate(path.read_text(encoding='utf-8-sig').splitlines(), 1):
            if not line.strip() or line.lstrip().startswith('#'):
                continue
            parts = [p.strip().replace('\\n','\n') for p in line.split('|')]
            for j, value in enumerate(parts[1:3] if name.endswith('TutorialTexts') else parts[1:2]):
                key = 'tutorial.' + parts[0].lower() + ('.title' if j == 0 else '.body') if name.endswith('TutorialTexts') else 'room.' + parts[0]
                yield ('copy',path.as_posix(),str(i),'',key,value)
    with tempfile.TemporaryDirectory() as temp:
        file = Path(temp) / 'code.csv'
        subprocess.run(['pwsh','-NoProfile','-File',str(Path(__file__).with_name('collect-code.ps1')),
                        '-OutputPath',str(file)],check=True)
        with file.open(encoding='utf-8-sig',newline='') as stream:
            for row in csv.DictReader(stream):
                yield ('code candidate',row['asset'],row['location'],'',key_for(row['source']),row['source'])

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('output',type=Path)
    args = parser.parse_args()
    entries = []
    for catalog in sorted(Path('Assets/Resources/Localization').glob('*.csv')):
        with catalog.open(encoding='utf-8-sig', newline='') as file:
            entries.extend(csv.DictReader(file))
    if not entries:
        raise ValueError('No localization CSV files found')
    resources = Path('Assets/Resources')
    def mapping(name):
        return dict(line.split('\t') for line in (resources / name).read_text(encoding='utf-8-sig').splitlines())
    aliases = mapping('LocalizationKeyAliases.txt')
    arguments = mapping('LocalizationArguments.txt')
    bindings = set(arguments.values()) | {'COMMON_VALUE_CURRENT'}
    particles = {p: '(' + pair + ')' for pair in ('을/를','이/가','은/는','과/와','으로/로','이다/다')
                 for p in pair.split('/')}
    def sheet_source(source):
        return re.sub(r'\{([\w.· -]+)(?::([가-힣]+))?\}', lambda m: '{' +
                      arguments.get(m[1], aliases.get(m[1], m[1])) + '}' + particles.get(m[2], ''), source)
    keys = {r['key']: r['source'] for r in entries}
    first_key = {}
    for row in entries:
        if row['key'] not in bindings:
            first_key.setdefault(row['source'], row['key'])
    with args.output.open('w', encoding='utf-8-sig',newline='') as file:
        rows = csv.writer(file)
        rows.writerow(('kind','asset','location','field','suggested_key','source','catalog_key'))
        for kind, path, location, field, suggested, source in collect():
            key = aliases.get(suggested, suggested)
            normalized = sheet_source(source)
            matched = key if keys.get(key) == normalized else first_key.get(normalized, '')
            if source.startswith('@loc:'):
                explicit = source[len('@loc:'):]
                candidate = aliases.get(explicit, explicit)
                if candidate in keys:
                    matched = candidate
            rows.writerow((kind, path, location, field, suggested, source, matched))

if __name__ == '__main__':
    main()

