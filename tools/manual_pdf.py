#!/usr/bin/env python3
# -*- coding: utf-8 -*-
r"""
manual_pdf.py — 사용설명서 Markdown → A4 PDF (조사자 · 관리자 같은 스크립트 · 지시서 §26 · §30)

흐름
    Markdown(한 파일 또는 폴더의 *.md 이름 순) → markdown-it-py(표) → HTML + manual_pdf.css
    → Edge 헤드리스 인쇄(CSS 쪽 번호) → pypdf 로 목차 링크가 가리키는 쪽을 읽어 목차에 넣고 한 번 더 인쇄
    → pypdf 로 책갈피(목차와 같음 · 장 > 절) · 메타데이터(제목 · 판 · 만든 곳 · 날짜)를 넣는다.
    인쇄가 끝나면 이 스크립트가 띄운 브라우저 프로세스(전용 프로필)를 모두 닫고 프로필을 지운다.

원고 규칙
    - 첫 파일 맨 앞 앞머리(--- … ---): title · subtitle · version · app · date · org · issued(yyyy-mm-dd) · notice
      issued 는 PDF 메타데이터 날짜(없으면 date 의 연 · 월 1일). 같은 원고 · 그림이면 같은 PDF 가 나온다.
    - '# 3. 장 제목'(장 · 새 쪽) · '## 3.1. 절 제목' — 번호는 원고에 적는다. 목차 · 책갈피 = 장 + 절.
      부록은 '# 부록 A. 제목'.
    - 문단에 그림 하나만 있으면 그림: ![제목](img/파일.jpg) → '그림 3-5. 제목'(장-순번 · 자동).
      그림 바로 다음 표는 강조 번호 설명표(가운데 · 좁게 · 칸에 ① 하나만 있으면 그림 속 표시처럼 빨간 원 번호).
      본문에서 그림을 가리킬 때는 {{그림 파일이름}}(확장자 뺀 이름) → '그림 3-5'.
    - '> ⚠ …' = 주의 상자 · '> …' = 앱 문구 인용.
    - 글꼴은 --font-dir 의 Pretendard(Regular · SemiBold · Bold · OFL) 하나만 쓴다(코드 글자도).

사용법
    python tools/manual_pdf.py <원고 .md 또는 폴더> <출력.pdf> [--font-dir <Pretendard 폴더>]
                               [--css <css>] [--browser <msedge.exe>] [--html <디버그용 html 경로>]
    --font-dir 를 빼면 원고 폴더의 fonts\ → 이 스크립트 옆 ..\SDSM_Surveyor_App\Assets\Fonts 순으로 찾는다.
    필요한 것: Python 3 · markdown-it-py · pypdf · Microsoft Edge(또는 Chrome). 새로 설치할 것 없음.
검사(끝나면 늘 돈다 · 하나라도 어긋나면 종료코드 1)
    책갈피 = 목차 · 목차 쪽 번호 = 실제 쪽 · 글꼴 = Pretendard 만 · 내부 링크가 모두 쪽을 가리킴
    · 그림 파일이 모두 있음 · {{그림 …}} 가 모두 풀림 · 본문 글자를 뽑을 수 있음.
"""
import argparse
import datetime
import html
import io
import pathlib
import re
import shutil
import subprocess
import sys
import tempfile
import time

from markdown_it import MarkdownIt
from markdown_it.token import Token
from pypdf import PdfReader, PdfWriter
from pypdf.generic import NameObject

# 출력을 파일로 돌려도 '—' '·' 에서 죽지 않게(cp949 콘솔)
if sys.stdout.encoding.lower() != "utf-8":
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

HERE = pathlib.Path(__file__).resolve().parent
BROWSERS = (
    r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
    r"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
    r"C:\Program Files\Google\Chrome\Application\chrome.exe",
)
FONT_FILES = (("Pretendard-Regular.ttf", 400), ("Pretendard-SemiBold.ttf", 600), ("Pretendard-Bold.ttf", 700))
FIG_REF = re.compile(r"\{\{그림\s+([^}\s]+)\s*\}\}")
CHAPTER_KEY = re.compile(r"^(?:부록\s*)?([0-9]+|[A-Z])\.")


class BuildError(Exception):
    pass


# ── 원고 읽기 ────────────────────────────────────────────────────
def read_sources(src):
    """(앞머리, [(파일 경로, 본문)]) — 폴더면 *.md 이름 순(이름이 '_' 로 시작하면 뺌)."""
    src = pathlib.Path(src).resolve()
    files = [src] if src.is_file() else sorted(p for p in src.glob("*.md") if not p.name.startswith("_"))
    if not files:
        raise BuildError(f"원고가 없습니다: {src}")
    meta, parts = {}, []
    for f in files:
        text = f.read_text(encoding="utf-8").replace("\r\n", "\n")
        m = re.match(r"^---\n(.*?)\n---\n", text, re.S)
        if m:
            for line in m.group(1).splitlines():
                if ":" in line:
                    k, v = line.split(":", 1)
                    meta.setdefault(k.strip(), v.strip())
            text = text[m.end():]
        parts.append((f, text))
    return meta, parts


def issued_date(meta):
    s = meta.get("issued") or meta.get("date", "")
    m = re.search(r"(\d{4})\D+(\d{1,2})(?:\D+(\d{1,2}))?", s)
    if not m:
        raise BuildError("앞머리에 issued(yyyy-mm-dd) 나 date 가 없습니다 — PDF 날짜를 정할 수 없음")
    return datetime.date(int(m.group(1)), int(m.group(2)), int(m.group(3) or 1))


# ── Markdown → HTML ─────────────────────────────────────────────
class Doc:
    def __init__(self):
        self.heads = []      # (tag, id, 글자)
        self.figures = {}    # 이름 → (번호, 제목, 파일)
        self.missing = []    # 없는 그림 파일
        self.body = []


def render_part(md, doc, path, text, state):
    tokens = md.parse(text)
    out = []
    i = 0
    after_figure = False  # 그림 바로 다음 표 = 강조 번호 설명표
    while i < len(tokens):
        t = tokens[i]
        if t.type == "heading_open" and t.tag in ("h1", "h2"):
            title = tokens[i + 1].content.strip()
            if t.tag == "h1":
                state["ch"] += 1
                state["sec"] = 0
                m = CHAPTER_KEY.match(title)
                state["key"] = m.group(1) if m else str(state["ch"])
                state["fig"] = 0
                hid = f"c{state['ch']}"
            else:
                state["sec"] += 1
                hid = f"c{state['ch']}-{state['sec']}"
            t.attrSet("id", hid)
            doc.heads.append((t.tag, hid, title))
        # 문단에 그림 하나만 → figure
        if (t.type == "paragraph_open" and i + 2 < len(tokens) and tokens[i + 1].type == "inline"
                and len(tokens[i + 1].children or []) == 1 and tokens[i + 1].children[0].type == "image"):
            img = tokens[i + 1].children[0]
            src = img.attrGet("src")
            alt = "".join(c.content for c in img.children or []) or img.content
            f = (path.parent / src).resolve()
            if not f.exists():
                doc.missing.append(f"{path.name}: {src}")
            state["fig"] += 1
            num = f"{state['key']}-{state['fig']}"
            name = pathlib.Path(src).stem
            if name in doc.figures:
                raise BuildError(f"같은 그림을 두 번 넣었습니다: {src}({path.name}) — 두 번째부터는 {{{{그림 {name}}}}} 로 가리킵니다")
            doc.figures[name] = (num, alt, f)
            fig = Token("html_block", "", 0, block=True)
            fig.content = (f'<figure id="fig-{html.escape(name)}"><img src="{f.as_uri()}" alt="{html.escape(alt)}">'
                           f"<figcaption>그림 {num}. {html.escape(alt)}</figcaption></figure>\n")
            out.append(fig)
            i += 3
            after_figure = True
            continue
        if t.type == "table_open":
            heads, j = 0, i + 1
            while tokens[j].type != "tr_close":
                heads += tokens[j].type == "th_open"
                j += 1
            t.attrSet("class", "legend" if after_figure else ("kv" if heads == 2 else "grid"))
        if t.type == "blockquote_open":
            inline = next((x for x in tokens[i + 1:i + 4] if x.type == "inline"), None)
            if inline is not None and inline.content.lstrip().startswith("⚠"):
                t.attrSet("class", "warn")
        if t.block and t.nesting >= 0 and t.level == 0:
            after_figure = False
        out.append(t)
        i += 1
    doc.body.append(md.renderer.render(out, md.options, {}))


def build_html(meta, parts, css, font_dir, pages=None):
    md = MarkdownIt("commonmark", {"html": True}).enable("table")
    doc = Doc()
    state = {"ch": 0, "sec": 0, "key": "0", "fig": 0}
    for path, text in parts:
        render_part(md, doc, path, text, state)
    body = "\n".join(doc.body)

    unresolved = []

    def ref(m):
        f = doc.figures.get(m.group(1))
        if not f:
            unresolved.append(m.group(1))
            return m.group(0)
        return f"그림 {f[0]}"
    body = FIG_REF.sub(ref, body)
    # 강조 번호 설명표의 ①② 는 그림 속 표시처럼 빨간 원 + 흰 숫자로(① 글자는 작게 찍힌다)
    body = re.sub(r"<td>([①-⑳])</td>", lambda m: f'<td><span class="num">{ord(m.group(1)) - 0x245F}</span></td>', body)

    pages = pages or {}
    toc = "".join(
        f'<li class="{"l1" if tag == "h1" else "l2"}"><a href="#{hid}">{html.escape(title)}</a>'
        f'<span class="dots"></span><span class="pg">{pages.get(hid, "00")}</span></li>'
        for tag, hid, title in doc.heads)
    e = lambda k: html.escape(meta.get(k, ""))
    lines = "".join(f'<p class="line">{e(k)}</p>' for k in ("version", "app", "date") if meta.get(k))
    notice = meta.get("notice") or "글꼴: Pretendard(SIL Open Font License 1.1)"
    cover = (f'<section class="cover"><div class="top"><p class="title">{e("title")}</p>'
             + (f'<p class="subtitle">{e("subtitle")}</p>' if meta.get("subtitle") else "")
             + f'{lines}</div><div><p class="org">{e("org")}</p><p class="notice">{html.escape(notice)}</p></div></section>')
    faces = "".join(
        f"@font-face {{ font-family: 'Pretendard'; font-weight: {w}; src: url('{(font_dir / n).as_uri()}'); }}\n"
        for n, w in FONT_FILES)
    page = (f'<!doctype html><html lang="ko"><head><meta charset="utf-8"><title>{e("title")}</title>'
            f"<style>{faces}{css}</style></head><body>{cover}"
            f'<nav class="toc"><p class="toc-title">목차</p><ol>{toc}</ol></nav>{body}</body></html>')
    return doc, unresolved, page


# ── 인쇄 ────────────────────────────────────────────────────────
def kill_profile_processes(profile):
    """전용 프로필로 뜬 브라우저 프로세스만 닫는다(사용자가 띄운 Edge 는 건드리지 않음)."""
    ps = ("Get-CimInstance Win32_Process -Filter \"Name='msedge.exe' or Name='chrome.exe'\" | "
          f"Where-Object {{ $_.CommandLine -like '*{profile}*' }} | "
          "ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue; $_.ProcessId }")
    r = subprocess.run(["powershell", "-NoProfile", "-NonInteractive", "-Command", ps],
                       capture_output=True, text=True, encoding="utf-8", errors="replace")
    return [x for x in r.stdout.split() if x.strip()]


def print_pdf(browser, html_path, pdf_path, profile):
    cmd = [browser, "--headless", "--disable-gpu", "--no-pdf-header-footer", "--no-first-run",
           "--no-default-browser-check", "--disable-extensions", f"--user-data-dir={profile}",
           "--allow-file-access-from-files", f"--print-to-pdf={pdf_path}", html_path.as_uri()]
    pdf_path.unlink(missing_ok=True)
    # Edge 는 PDF 를 쓰고도 끝나지 않는다 → PDF 가 2초 동안 그대로면 프로세스 트리를 닫는다(출력 파이프는 잡지 않음)
    p = subprocess.Popen(cmd, stdin=subprocess.DEVNULL, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    t0, last = time.time(), None
    while p.poll() is None and time.time() - t0 < 180:
        if pdf_path.exists() and pdf_path.stat().st_size > 0:
            sig = (pdf_path.stat().st_size, pdf_path.stat().st_mtime_ns)
            if sig == last:
                break
            last = sig
        time.sleep(2)
    if p.poll() is None:
        subprocess.run(["taskkill", "/T", "/F", "/PID", str(p.pid)], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    left = kill_profile_processes(profile)
    if not pdf_path.exists() or pdf_path.stat().st_size == 0:
        raise BuildError(f"PDF 가 만들어지지 않았습니다({browser})")
    return time.time() - t0, left


def dest_pages(reader):
    """이름 붙은 목적지(제목 id) → 쪽 번호(표지 = 1쪽)."""
    # Edge 는 /Dests 사전에 이름(/c1)으로 넣는다 — 앞 / 를 뗀다
    return {str(k).lstrip("/"): reader.get_destination_page_number(d) + 1 for k, d in reader.named_destinations.items()}


# ── 마무리(책갈피 · 메타데이터) ─────────────────────────────────
def finish(tmp_pdf, out, doc, pages, meta, day):
    w = PdfWriter(clone_from=str(tmp_pdf))
    parent = None
    for tag, hid, title in doc.heads:
        item = w.add_outline_item(title, pages[hid] - 1, parent=None if tag == "h1" else parent)
        if tag == "h1":
            parent = item
    w._root_object[NameObject("/PageMode")] = NameObject("/UseOutlines")
    stamp = f"D:{day:%Y%m%d}000000+09'00'"
    w.metadata = None
    w.add_metadata({
        "/Title": meta.get("title", ""),
        "/Subject": " · ".join(meta[k] for k in ("subtitle", "version", "app") if meta.get(k)),
        "/Author": meta.get("org", ""),
        "/Creator": "tools/manual_pdf.py",
        "/Producer": "markdown-it-py + Edge + pypdf",
        "/CreationDate": stamp,
        "/ModDate": stamp,
    })
    with open(out, "wb") as f:
        w.write(f)


# ── 검사 ────────────────────────────────────────────────────────
def check(out, doc, unresolved):
    r = PdfReader(str(out))
    problems = []
    pages = dest_pages(r)
    # 책갈피 = 목차
    marks = []

    def walk(items, depth):
        for o in items:
            if isinstance(o, list):
                walk(o, depth + 1)
            else:
                marks.append(("h1" if depth == 0 else "h2", o.title, r.get_destination_page_number(o) + 1))
    walk(r.outline, 0)
    toc = [(tag, title, pages.get(hid)) for tag, hid, title in doc.heads]
    if marks != toc:
        problems.append(f"책갈피 {len(marks)}개가 목차 {len(toc)}개와 다릅니다")
    # 목차에 찍힌 쪽 번호 = 실제(목차 쪽 글자에서 줄마다 '제목 쪽' 을 읽음 · 두 줄로 접힌 제목은 이어 붙임)
    first_body = min(pages.values()) if pages else 2
    norm = lambda x: re.sub(r"\s+", "", x)
    printed, buf = [], ""
    for i in range(1, first_body - 1):
        for line in (r.pages[i].extract_text() or "").splitlines():
            line = line.strip()
            if not line or line == "목차" or (line.isdigit() and not buf):  # 바닥글 쪽 번호
                continue
            m = re.match(r"^(.*?)\s*(\d+)$", line)
            if m and m.group(1):
                printed.append((norm(buf + m.group(1)), int(m.group(2))))
                buf = ""
            else:
                buf += line
    expected = [(norm(title), pages.get(hid)) for _, hid, title in doc.heads]
    if printed != expected:
        wrong = [f"{e[0]}(목차 {p[1] if p else '?'} · 실제 {e[1]})" for e, p in zip(expected, printed + [None] * len(expected)) if p != e]
        problems.append(f"목차 쪽 번호가 실제와 다름({len(wrong)}곳): " + " / ".join(wrong[:5]))
    # 글꼴
    fonts = set()
    for p in r.pages:
        res = p.get("/Resources") or {}
        for f in (res.get("/Font") or {}).values():
            fonts.add(str(f.get_object().get("/BaseFont")).split("+")[-1])
    other = sorted(f for f in fonts if not f.startswith("Pretendard"))
    if other:
        problems.append(f"Pretendard 밖 글꼴: {other}")
    # 내부 링크
    broken = 0
    for p in r.pages:
        for a in p.get("/Annots") or []:
            a = a.get_object()
            if a.get("/Subtype") != "/Link":
                continue
            d = a.get("/Dest")
            act = a.get("/A")
            if d is None and act is not None and act.get("/S") == "/GoTo":
                d = act.get("/D")
            if d is None and act is not None and act.get("/S") == "/URI":
                continue
            if d is None or str(d).lstrip("/") not in pages:
                broken += 1
    if broken:
        problems.append(f"깨진 내부 링크 {broken}개")
    if doc.missing:
        problems.append("없는 그림 파일: " + ", ".join(doc.missing))
    if unresolved:
        problems.append("풀리지 않은 그림 가리킴: " + ", ".join(sorted(set(unresolved))))
    sample = "".join((r.pages[i].extract_text() or "") for i in range(min(len(r.pages), first_body + 1)))
    if not re.search("[가-힣]", sample):
        problems.append("본문 한글을 뽑지 못합니다(검색 불가)")
    return r, sorted(fonts), marks, problems


def main():
    ap = argparse.ArgumentParser(description="사용설명서 Markdown → A4 PDF")
    ap.add_argument("src", help="원고 .md 또는 *.md 가 든 폴더")
    ap.add_argument("out", help="출력 PDF")
    ap.add_argument("--font-dir", help="Pretendard-Regular/SemiBold/Bold.ttf 폴더")
    ap.add_argument("--css", default=str(HERE / "manual_pdf.css"))
    ap.add_argument("--browser", default=next((b for b in BROWSERS if pathlib.Path(b).exists()), None))
    ap.add_argument("--html", help="인쇄한 HTML 을 여기에도 남김(디버그)")
    a = ap.parse_args()

    src = pathlib.Path(a.src).resolve()
    out = pathlib.Path(a.out).resolve()
    base = src if src.is_dir() else src.parent
    cands = [pathlib.Path(a.font_dir)] if a.font_dir else [base / "fonts", HERE.parent / "SDSM_Surveyor_App" / "Assets" / "Fonts"]
    font_dir = next((c.resolve() for c in cands if all((c / n).exists() for n, _ in FONT_FILES)), None)
    if font_dir is None:
        raise BuildError("Pretendard 글꼴 폴더를 찾지 못했습니다: " + " · ".join(str(c) for c in cands))
    if not (font_dir / "OFL.txt").exists():
        print(f"주의: {font_dir} 에 OFL.txt 가 없습니다(글꼴 사용 허락 표기)")
    if not a.browser or not pathlib.Path(a.browser).exists():
        raise BuildError("Edge(또는 Chrome)를 찾지 못했습니다 — --browser 로 알려 주세요")

    meta, parts = read_sources(src)
    day = issued_date(meta)
    css = pathlib.Path(a.css).read_text(encoding="utf-8")
    work = pathlib.Path(tempfile.mkdtemp(prefix="manual_pdf_"))
    profile = work / "profile"
    html_path, tmp_pdf = work / "manual.html", work / "print.pdf"
    t0 = time.time()
    try:
        pages, runs, left_all = None, 0, []
        while True:
            runs += 1
            doc, unresolved, page = build_html(meta, parts, css, font_dir, pages)
            html_path.write_text(page, encoding="utf-8")
            if a.html:
                shutil.copyfile(html_path, a.html)
            _, left = print_pdf(a.browser, html_path, tmp_pdf, profile)
            left_all += left
            got = dest_pages(PdfReader(str(tmp_pdf)))
            got = {hid: got.get(hid) for _, hid, _ in doc.heads}
            if None in got.values():
                raise BuildError("목차 링크가 가리키는 쪽을 읽지 못했습니다: "
                                 + ", ".join(h for h, v in got.items() if v is None))
            if got == pages or runs >= 4:
                break
            pages = got
        out.parent.mkdir(parents=True, exist_ok=True)
        finish(tmp_pdf, out, doc, pages, meta, day)
    finally:
        kill_profile_processes(profile)
        for _ in range(5):
            try:
                shutil.rmtree(work)
                break
            except OSError:
                time.sleep(1)

    r, fonts, marks, problems = check(out, doc, unresolved)
    print(f"PDF   {out}")
    print(f"쪽    {len(r.pages)} · 크기 {out.stat().st_size / 1048576:.2f} MB · 인쇄 {runs}회 · {time.time() - t0:.0f}초")
    print(f"목차  장 {sum(1 for h in doc.heads if h[0] == 'h1')} · 절 {sum(1 for h in doc.heads if h[0] == 'h2')} · 책갈피 {len(marks)}")
    print(f"그림  {len(doc.figures)}")
    print(f"글꼴  {', '.join(fonts)}")
    print(f"날짜  {day} · 브라우저 {a.browser}" + (f" · 닫은 남은 프로세스 {len(left_all)}" if left_all else ""))
    if runs >= 4 and pages != got:
        problems.append("목차 쪽 번호가 4번 인쇄해도 자리 잡지 않았습니다")
    for p in problems:
        print("문제  " + p)
    print("검사  " + ("통과" if not problems else f"실패 {len(problems)}건"))
    return 1 if problems else 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except BuildError as ex:
        print("중단  " + str(ex))
        sys.exit(2)
