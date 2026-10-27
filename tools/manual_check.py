#!/usr/bin/env python3
# -*- coding: utf-8 -*-
r"""
manual_check.py — 조사자 사용설명서 원고 검사(지시서 §26 · S3)

1. 원고의 [단추 · 창 이름] · 「칸 이름 · 화면 문구」 를 앱 코드(SDSM_Surveyor_App 의 xaml · cs 글자)와 대조한다.
   - [ ] 는 코드 글자 하나와 **통째로** 같아야 한다(단추 글자는 글자 하나이므로 · '보고서용' 이 긴 글 속에 있다고 통과하지 않음).
   - 「 」 는 코드 글자 하나(또는 + 로 이은 글자들)의 **일부**면 된다(긴 문구는 앞 한 문장만 따와도 됨).
   - 원고의 {지점} 같은 자리는 아무 글자, 코드의 $"…{식}…" · string.Format {0} 자리도 아무 글자로 본다.
   - 앱 글자가 아닌데 대괄호를 쓰는 것(Windows 메시지 상자 단추 · 아이콘 단추)은 아래 ALLOW 에 까닭과 함께만.
2. 그림: ![제목](img/이름.jpg) 파일이 있는지 · 한 번만 넣었는지 · 바로 다음에 강조 번호 설명표가 있는지 · {{그림 이름}} 이 다 풀리는지
   · 그림 수(--figures, 기본 34).
사용법
    python tools/manual_check.py [원고.md] [--figures 34]
    (기본 원고: docs/manual/조사자_사용설명서.md) — 하나라도 어긋나면 종료코드 1.
"""
import argparse
import html
import io
import pathlib
import re
import sys

if sys.stdout.encoding.lower() != "utf-8":
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

ROOT = pathlib.Path(__file__).resolve().parent.parent
APP = ROOT / "SDSM_Surveyor_App"

# 앱 코드에 글자로 없는데 원고에 대괄호로 쓰는 것 — 까닭을 같이 적는다
ALLOW = {
    "예": "Windows 메시지 상자 단추(앱 코드가 아니라 Windows 가 붙이는 글자)",
    "아니요": "Windows 메시지 상자 단추",
    "확인": "Windows 메시지 상자 단추",
    "취소": "Windows 메시지 상자 단추",
    "×": "알림 띠 닫기 — 글자 없는 아이콘 단추(툴팁 「알림 닫기」)",
}


# ── 코드 글자 모으기 ─────────────────────────────────────────────
HOLE = "⁣"   # 코드의 {식} 자리 표시(보이지 않는 글자)
ESC = {"n": "\n", "t": "\t", "r": "", "\\": "\\", '"': '"', "'": "'", "0": ""}


def cs_literals(text):
    """C# 글자 상수를 모두 꺼낸다 — $"…{식}…" 의 식 자리는 HOLE, 식 안의 글자 상수도 따로 꺼낸다.
    + 로 이은 글자 상수는 이어 붙인 것도 하나 더(사이에 변수가 끼면 HOLE)."""
    out, i, n = [], 0, len(text)
    chain, last_end = [], -1

    def flush():
        if len(chain) > 1:
            out.append("".join(chain)); out.append(HOLE.join(chain))
        chain.clear()

    def read(i, interp, verbatim):
        """i = 여는 따옴표 다음. (글자, 닫는 따옴표 다음 위치)."""
        buf = []
        while i < n:
            ch = text[i]
            if verbatim and ch == '"':
                if i + 1 < n and text[i + 1] == '"':
                    buf.append('"'); i += 2; continue
                return "".join(buf), i + 1
            if not verbatim and ch == "\\" and i + 1 < n:
                e = text[i + 1]
                if e == "u" and i + 5 < n:
                    buf.append(chr(int(text[i + 2:i + 6], 16))); i += 6; continue
                buf.append(ESC.get(e, e)); i += 2; continue
            if not verbatim and ch in '"\n':
                return "".join(buf), i + 1
            if interp and ch == "{":
                if i + 1 < n and text[i + 1] == "{":
                    buf.append("{"); i += 2; continue
                # 식 — 괄호 깊이를 세며 끝까지, 안의 글자 상수도 꺼냄
                depth, i = 1, i + 1
                start = i
                while i < n and depth:
                    c = text[i]
                    if c == "{": depth += 1
                    elif c == "}": depth -= 1
                    elif c == '"' or (c in "$@" and i + 1 < n and text[i + 1] in '"$@'):
                        j = i
                        while j < n and text[j] in "$@": j += 1
                        pre = text[i:j]
                        s2, i = read(j + 1, "$" in pre, "@" in pre)
                        out.append(s2); continue
                    elif c == "'":
                        i = text.index("'", i + 2 if text[i + 1] == "\\" else i + 1) + 1 if i + 1 < n else n; continue
                    i += 1
                expr = text[start:i - 1].split(":")[0].strip().split(".")[-1]
                buf.append(CONSTS.get(expr, HOLE)); continue   # {AppInfo.ProgramName} 같은 상수는 글자로
            if interp and ch == "}" and i + 1 < n and text[i + 1] == "}":
                buf.append("}"); i += 2; continue
            buf.append(ch); i += 1
        return "".join(buf), i

    while i < n:
        ch = text[i]
        if text.startswith("//", i):
            i = text.find("\n", i); i = n if i < 0 else i; continue
        if text.startswith("/*", i):
            i = text.find("*/", i); i = n if i < 0 else i + 2; continue
        if ch == "'":
            if i + 2 < n and text[i + 1] == "\\":
                i = text.find("'", i + 3) + 1
            else:
                i = i + 3
            continue
        if ch == '"' or (ch in "$@" and i + 1 < n and text[i + 1] in '"$@'):
            j = i
            while j < n and text[j] in "$@": j += 1
            if j >= n or text[j] != '"':
                i += 1; continue
            pre = text[i:j]
            lit, end = read(j + 1, "$" in pre, "@" in pre)
            lit = re.sub(r"\{\d+(?::[^{}]*)?\}", HOLE, lit)   # string.Format {0}
            between = text[last_end:i] if last_end >= 0 else ""
            if chain and re.fullmatch(r"\s*\+\s*", between):
                chain.append(lit)
            elif chain and re.fullmatch(r"\s*\+\s*[\w.?()\[\]]+\s*\+\s*", between):
                chain.append(HOLE + lit)   # "…" + 변수 + "…"
            else:
                flush(); chain.append(lit)
            out.append(lit)
            last_end, i = end, end
            continue
        i += 1
    flush()
    return out


def xaml_format(v):
    """{Binding …, StringFormat=합계 {0}종} 의 StringFormat 글자."""
    out = []
    for m in re.finditer(r"StringFormat=", v):
        k = m.end()
        if k < len(v) and v[k] == "'":
            out.append(v[k + 1:v.index("'", k + 1)]); continue
        depth, buf = 0, []
        while k < len(v):
            c = v[k]
            if c == "{": depth += 1
            elif c == "}":
                if depth == 0: break
                depth -= 1
            elif c == "," and depth == 0: break
            buf.append(c); k += 1
        out.append("".join(buf).strip())
    return out


CONSTS = {}


def code_strings():
    out = set()
    for f in APP.rglob("*.cs"):
        if not any(p in ("bin", "obj") for p in f.parts):
            for m in re.finditer(r'const\s+string\s+(\w+)\s*=\s*"((?:[^"\\]|\\.)*)"\s*;', f.read_text(encoding="utf-8-sig", errors="replace")):
                CONSTS[m.group(1)] = m.group(2)
    for f in APP.rglob("*.cs"):
        if any(p in ("bin", "obj") for p in f.parts):
            continue
        out.update(cs_literals(f.read_text(encoding="utf-8-sig", errors="replace")))
    for f in APP.rglob("*.xaml"):
        if any(p in ("bin", "obj") for p in f.parts):
            continue
        text = f.read_text(encoding="utf-8-sig", errors="replace")
        text = re.sub(r"<!--.*?-->", "", text, flags=re.S)
        for m in re.finditer(r'[\w:.]+\s*=\s*"([^"]*)"', text):
            v = html.unescape(m.group(1))
            vals = xaml_format(v) if v.startswith("{") and not v.startswith("{}") else [v[2:] if v.startswith("{}") else v]
            for s in vals:
                out.add(re.sub(r"\{\d+(?::[^{}]*)?\}", HOLE, s.replace("{}", "", 1)))
        for m in re.finditer(r">([^<>]+)<", text):
            v = html.unescape(m.group(1)).strip()
            if v:
                out.add(v)
    # 끝 띄어쓰기도 남겨 둔다("… — " + 까닭 처럼 뒤에 이어 붙는 글자)
    return {s.strip() for s in out if s.strip()} | {s for s in out if s.strip() and s != s.strip()}


def to_regex(token):
    """원고 글자 → 정규식({자리} · 코드 HOLE 은 아무 글자 · 띄어쓰기는 그대로)."""
    parts = re.split(r"(\{[^{}]+\}|…)", token)
    rx = "".join(".*?" if (p.startswith("{") and p.endswith("}")) or p == "…" else re.escape(p) for p in parts)
    return rx


def code_regex(s):
    return "".join(".*?" if ch == HOLE else re.escape(ch) for ch in s)


class Matcher:
    def __init__(self, strings):
        self.strings = strings
        self.plain = "\n".join(s.replace(HOLE, "\x00") for s in strings)
        self.holes = [s for s in strings if HOLE in s]

    def button(self, t):
        if t in self.strings:
            return True
        rx = re.compile(to_regex(t) + r"\Z")
        if any(rx.match(s.replace(HOLE, "X")) for s in self.strings if len(s) < 200):
            return True
        # 코드의 {식} 자리를 뺀 글자가 반은 넘어야(자리만 있는 $"{x}" 가 아무 글자나 받지 않게)
        return any(len(s.replace(HOLE, "")) * 2 >= len(t) and re.fullmatch(code_regex(s), t) for s in self.holes if len(s) < 200)

    def text(self, t):
        if "{" not in t and "…" not in t and t in self.plain:
            return True
        rx = re.compile(to_regex(t))
        if rx.search(self.plain.replace("\x00", "X")):
            return True
        # 원고는 실제 값을 쓰고 코드는 {식} 자리인 경우(예 '오류 — 삼성 DS 모니터링 야장' ↔ $"오류 — {AppInfo.ProgramName}")
        return any(len(s.replace(HOLE, "")) >= 3 and len(s.replace(HOLE, "")) * 3 >= len(t) and re.fullmatch(code_regex(s), t)
                   for s in self.holes if len(s) < 300)


# ── 원고 글자 뽑기 ───────────────────────────────────────────────
def strip_code(line):
    return re.sub(r"`[^`]*`", " ", line)


def quotes(line):
    """「 」 바깥쪽만(안에 「 」 가 또 있어도 한 덩어리)."""
    out, depth, start = [], 0, 0
    for i, ch in enumerate(line):
        if ch == "「":
            if depth == 0:
                start = i + 1
            depth += 1
        elif ch == "」" and depth:
            depth -= 1
            if depth == 0:
                out.append(line[start:i])
    return out


def brackets(line):
    line = re.sub(r"!\[[^\]]*\]\([^)]*\)", " ", line)   # 그림
    line = re.sub(r"「[^「」]*」", lambda m: m.group(0).replace("[", "⟦").replace("]", "⟧"), line)  # 문구 안 대괄호는 문구로
    return [m.group(1) for m in re.finditer(r"\[([^\[\]]+)\]", line)]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("src", nargs="?", default=str(ROOT / "docs" / "manual" / "조사자_사용설명서.md"))
    ap.add_argument("--figures", type=int, default=34)
    a = ap.parse_args()
    src = pathlib.Path(a.src)
    lines = src.read_text(encoding="utf-8").splitlines()
    m = Matcher(code_strings())

    bad, n_btn, n_txt, allowed = [], 0, 0, {}
    for no, raw in enumerate(lines, 1):
        line = strip_code(raw)
        for q in quotes(line):
            n_txt += 1
            if not m.text(q.strip()):
                bad.append((no, "「」", q))
        for b in brackets(line):
            n_btn += 1
            b = b.strip()
            if b in ALLOW:
                allowed[b] = allowed.get(b, 0) + 1
                continue
            if not m.button(b):
                bad.append((no, "[ ]", b))

    # 그림
    probs = []
    figs = []
    for i, raw in enumerate(lines):
        g = re.fullmatch(r"\s*!\[([^\]]+)\]\(([^)]+)\)\s*", raw)
        if g:
            figs.append((i + 1, g.group(1), g.group(2)))
            nxt = next((l for l in lines[i + 1:] if l.strip()), "")
            if not nxt.startswith("| 번호"):
                probs.append(f"{i + 1}줄 그림 '{g.group(2)}' 다음에 강조 번호 설명표가 없음")
    names = [pathlib.Path(p).stem for _, _, p in figs]
    for _, _, p in figs:
        if not (src.parent / p).exists():
            probs.append(f"그림 파일 없음: {p}")
    dup = sorted({n for n in names if names.count(n) > 1})
    if dup:
        probs.append(f"두 번 넣은 그림: {dup}")
    if len(figs) != a.figures:
        probs.append(f"그림 {len(figs)}장(기대 {a.figures})")
    refs = re.findall(r"\{\{그림\s+([^}\s]+)\s*\}\}", "\n".join(lines))
    miss = sorted(set(r for r in refs if r not in names))
    if miss:
        probs.append(f"풀리지 않는 그림 가리킴: {miss}")

    print(f"원고  {src}")
    print(f"대조  [ ] {n_btn}개 · 「 」 {n_txt}개 · 코드 글자 {len(m.strings)}개")
    print("허용  " + (" · ".join(f"[{k}] {v}회({ALLOW[k]})" for k, v in allowed.items()) or "없음"))
    print(f"그림  {len(figs)}장 · 가리킴 {len(refs)}곳")
    for no, kind, t in bad:
        print(f"불일치 {no}줄 {kind} {t}")
    for p in probs:
        print("문제  " + p)
    ok = not bad and not probs
    print("검사  " + ("통과 — 불일치 0" if ok else f"실패 — 불일치 {len(bad)} · 그림 문제 {len(probs)}"))
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
