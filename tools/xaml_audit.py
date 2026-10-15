#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
xaml_audit.py — XAML 정적 점검 (SDSM_Surveyor)

컴파일도 통과하고 XML 파싱도 통과하지만 **실행 중에 터지는** 세 가지를 잡는다.

1. StaticResource 전방참조
   StaticResource 는 같은 사전에서 '앞에 정의된 것'만 찾는다. 뒤 키를 앞에서 쓰면
   조용히 DependencyProperty.UnsetValue 가 들어가고, 레이아웃 **측정 단계**에서
   "'{DependencyProperty.UnsetValue}'은(는) 'BorderThickness' 속성의 유효한 값이 아닙니다"
   로 터진다. XAML 로드 시점이 아니라 원인 찾기가 어렵다.

2. 미정의 StaticResource 키
   Colors.xaml·Styles.xaml·해당 파일 어디에도 없는 키.

3. XAML 안 #RRGGBB 리터럴 (design.md §1 — 토큰 키로만 쓴다)
   Colors.xaml·Styles.xaml 은 토큰을 정의하는 자리이므로 제외한다.

사용법
    python tools/xaml_audit.py
빌드 게이트
    문제가 하나라도 있으면 종료코드 1.
"""
import sys, os, re, io, glob
import xml.dom.minidom

# ── Windows 콘솔 인코딩 대응 ──────────────────────────────────────
# 출력을 파일로 리다이렉트하면 파이썬이 ANSI 코드페이지(cp949)로 인코딩하려 들어
# '—' '■' '✓' 같은 글자에서 UnicodeEncodeError 로 죽는다. UTF-8 로 못박는다.
try:
    sys.stdout.reconfigure(encoding='utf-8')
    sys.stderr.reconfigure(encoding='utf-8')
except Exception:
    pass

APP     = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'SDSM_Surveyor_App')
COLORS  = os.path.join(APP, 'Styles', 'Colors.xaml')
STYLES  = os.path.join(APP, 'Styles', 'Styles.xaml')
TOKEN_FILES = {os.path.normpath(COLORS), os.path.normpath(STYLES)}

KEY_RE  = re.compile(r'x:Key="([\w.]+)"')
USE_RE  = re.compile(r'\{StaticResource\s+([\w.]+)\s*\}')
HEX_RE  = re.compile(r'(?<![\w])#[0-9A-Fa-f]{6}(?:[0-9A-Fa-f]{2})?(?![\w])')


def read(p):
    return io.open(p, encoding='utf-8-sig').read()


def strip_comments(t):
    """주석 안의 설명용 hex 는 리터럴이 아니다."""
    return re.sub(r'<!--.*?-->', '', t, flags=re.S)


def main():
    files = sorted(glob.glob(os.path.join(APP, '**', '*.xaml'), recursive=True))
    files = [f for f in files if os.sep + 'obj' + os.sep not in f and os.sep + 'bin' + os.sep not in f]

    defined = set()
    for f in (COLORS, STYLES):
        defined |= set(KEY_RE.findall(read(f)))

    fwd, missing, literals, malformed = [], [], [], []

    for f in files:
        # ⓪ XML 자체가 깨졌는지. 컴파일러의 MC3000 을 여기서 먼저 잡는다.
        #    (스크립트로 속성을 일괄 삽입하다 속성 요소 태그 <telerik:RadX.Prop> 를
        #     건드리면 정확히 이 형태로 깨진다 — 실제로 한 번 겪었다.)
        try:
            xml.dom.minidom.parse(f)
        except Exception as e:
            malformed.append((os.path.relpath(f, os.path.join(APP, '..')), str(e).split('\n')[0]))
            continue

    for f in files:
        raw  = read(f)
        body = strip_comments(raw)
        rel  = os.path.relpath(f, os.path.join(APP, '..'))

        # ① 같은 파일 안 전방참조
        first_def = {}
        for m in KEY_RE.finditer(raw):
            first_def.setdefault(m.group(1), m.start())
        seen = set()
        for m in USE_RE.finditer(raw):
            k = m.group(1)
            if k in first_def and m.start() < first_def[k] and k not in seen:
                seen.add(k)
                fwd.append((rel, k))

        # ② 미정의 키
        local = set(first_def)
        for k in sorted(set(USE_RE.findall(raw)) - defined - local):
            missing.append((rel, k))

        # ③ hex 리터럴 (토큰 정의 파일은 제외)
        if os.path.normpath(f) not in TOKEN_FILES:
            for h in sorted(set(HEX_RE.findall(body))):
                literals.append((rel, h))

    print(f'■ XAML 정적 점검 — 파일 {len(files)}개 · 정의된 키 {len(defined)}개')
    print('-' * 78)

    def report(title, rows, hint):
        if not rows:
            print(f'  ✓ {title} : 없음')
            return
        print(f'  ✗ {title} : {len(rows)}건   ← {hint}')
        for rel, k in rows:
            print(f'      {rel}  :  {k}')

    report('XML 형식 오류', malformed, '태그가 깨졌다 — 컴파일 전에 반드시 고칠 것')
    report('StaticResource 전방참조', fwd, '토큰을 그 파일 앞쪽으로 옮길 것')
    report('미정의 StaticResource', missing, 'Colors/Styles 에 키를 만들 것')
    report('XAML 안 #RRGGBB 리터럴', literals, 'design.md §1 — 토큰 키로만 쓸 것')

    print('-' * 78)
    total = len(malformed) + len(fwd) + len(missing) + len(literals)
    print('통과' if total == 0 else f'문제 {total}건')
    return 1 if total else 0


if __name__ == '__main__':
    sys.exit(main())
