# Запускает программу с заранее заданным вводом и делает HTML-«консоли» для скриншотов отчёта.
import html, os, re, subprocess, sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = sys.argv[1]
PROMPT = re.compile(r'(Выберите пункт: |Номер сеанса: |Номер билета: |\(например 5-7 5-8\): )')

def run(cfg, lines):
    dll = os.path.join(ROOT, 'CinemaDebug', 'bin', cfg, 'net8.0', 'CinemaDebug.dll')
    log = os.path.join(os.path.dirname(dll), 'cinema_trace.log')
    if os.path.exists(log): os.remove(log)
    out = subprocess.run(['dotnet', dll], input='\n'.join(lines) + '\n', capture_output=True, text=True, encoding='utf-8').stdout
    it = iter(lines)
    # вставляем введённый текст после каждого приглашения, как это выглядит в настоящей консоли
    out = PROMPT.sub(lambda m: m.group(1) + '\x01' + next(it, '') + '\x02\n', out)
    return out, open(log, encoding='utf-8').read()

def page(name, title, text, width=900):
    body = html.escape(text).replace('\x01', '<b class=in>').replace('\x02', '</b>')
    doc = f'''<!doctype html><html><head><meta charset=utf-8><style>
body{{margin:0;background:#fff}} .win{{display:inline-block;width:{width}px;border:1px solid #3c3c3c;font-family:Consolas,"DejaVu Sans Mono",monospace}}
.bar{{background:#1f1f1f;color:#ddd;font:13px "Segoe UI",Arial,sans-serif;padding:7px 12px}}
pre{{margin:0;background:#0c0c0c;color:#cccccc;font-size:14px;line-height:1.35;padding:10px 12px;white-space:pre-wrap}}
.in{{color:#f9f1a5;font-weight:normal}}</style></head><body><div class=win id=shot><div class=bar>{html.escape(title)}</div><pre>{body}</pre></div></body></html>'''
    open(os.path.join(OUT, name + '.html'), 'w', encoding='utf-8').write(doc)

console = 'CinemaDebug — консоль'
MENU = '\n===== КИНОТЕАТР ====='

# 1. Афиша и схема зала
out, _ = run('Debug', ['1', '2', '2', '0'])
page('01_afisha', console, out.split('Журнал работы')[0].rstrip())

# 2. Покупка билетов и выручка
out, log_debug = run('Debug', ['3', '2', '5-7 5-8 5-9', '5', '0'])
parts = out.split(MENU)
page('02_pokupka', console, (MENU + parts[1] + MENU + parts[2]).strip())

# 3. Ошибки ввода
seq = ['3', '2', '5-7 5-8', '3', '2', '5-8', '3', '2', '9-1', '3', '2', 'абв', '3', '1', '1-1', '4', '1', '4', '1', '4', '7', '0']
out, log_full = run('Debug', seq)
blocks = out.split(MENU)[2:]
short = []
for b in blocks:
    b = re.sub(r'\n1 — Афиша.*?0 — Выход\n', '\n', b, flags=re.S)        # меню не повторяем
    b = re.sub(r'\n {7,}1  2  3.*?\(\. — свободно, X — занято\)\n', '\n[схема зала]\n', b, flags=re.S)
    short.append(b.strip())
page('03_oshibki', console, '\n\n'.join(x for x in short if 'Ошибка' in x or 'возвращён' in x))

# 4. Журналы Debug и Release
_, log_release = run('Release', seq)
open(os.path.join(OUT, 'log_debug.txt'), 'w', encoding='utf-8').write(log_full)
open(os.path.join(OUT, 'log_release.txt'), 'w', encoding='utf-8').write(log_release)
page('04_log_debug', 'cinema_trace.log — сборка Debug', log_full.strip(), 1000)
page('05_log_release', 'cinema_trace.log — сборка Release', log_release.strip(), 1000)
print('ok')
