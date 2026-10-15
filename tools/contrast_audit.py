#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
contrast_audit.py — SDSM_Surveyor 색 토큰 명도대비 감사 (WCAG 2.2 AA)

design.md §11 이 요구하는 대비 기준을 '문장'이 아니라 '코드의 실제 값'으로 검증한다.
Styles/Colors.xaml · Styles/Styles.xaml 을 직접 파싱하므로, 색을 고치면 결과가 바로 따라온다.

사용법
    python tools/contrast_audit.py                # 현행 감사
    python tools/contrast_audit.py --verbose      # 통과 항목까지 전부 출력
빌드 게이트
    미달이 하나라도 있으면 종료코드 1.
"""
import sys, os, re, argparse

# ── Windows 콘솔 인코딩 대응 ──────────────────────────────────────
# 출력을 파일로 리다이렉트하면 파이썬이 ANSI 코드페이지(cp949)로 인코딩하려 들어
# '—' '■' '✓' 같은 글자에서 UnicodeEncodeError 로 죽는다. UTF-8 로 못박는다.
try:
    sys.stdout.reconfigure(encoding='utf-8')
    sys.stderr.reconfigure(encoding='utf-8')
except Exception:
    pass

APP = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'SDSM_Surveyor_App')
COLORS = os.path.join(APP, 'Styles', 'Colors.xaml')
STYLES = os.path.join(APP, 'Styles', 'Styles.xaml')

# ── WCAG 상대 휘도 ────────────────────────────────────────────────
def _lin(c):
    c = c / 255.0
    return c / 12.92 if c <= 0.03928 else ((c + 0.055) / 1.055) ** 2.4

def parse(h):
    """'#RRGGBB' / '#AARRGGBB'(WPF) → (r, g, b, a)"""
    h = h.strip().lstrip('#')
    if len(h) == 8:
        return int(h[2:4],16), int(h[4:6],16), int(h[6:8],16), int(h[0:2],16)/255.0
    return int(h[0:2],16), int(h[2:4],16), int(h[4:6],16), 1.0

def composite(fg, bg, opacity=1.0):
    """알파(+ UIElement.Opacity)를 배경 위에 합성"""
    r,g,b,a = parse(fg); R,G,B,_ = parse(bg)
    a *= opacity
    return (round(r*a+R*(1-a)), round(g*a+G*(1-a)), round(b*a+B*(1-a)))

def lum(t):
    r,g,b = t[:3]
    return 0.2126*_lin(r) + 0.7152*_lin(g) + 0.0722*_lin(b)

def contrast(fg, bg, opacity=1.0):
    l1, l2 = lum(composite(fg,bg,opacity)), lum(parse(bg)[:3])
    if l1 < l2: l1, l2 = l2, l1
    return (l1+0.05) / (l2+0.05)

# ── XAML 파싱 ────────────────────────────────────────────────────
def load_colors(*paths):
    P = {}
    for p in paths:
        if not os.path.exists(p): continue
        txt = open(p, encoding='utf-8-sig').read()
        for k, v in re.findall(r'<Color\s+x:Key="([\w]+)"\s*>\s*(#[0-9A-Fa-f]{6,8})\s*</Color>', txt):
            P[k] = v
        for k, v in re.findall(r'<SolidColorBrush\s+x:Key="([\w]+)"\s+Color="(#[0-9A-Fa-f]{6,8})"', txt):
            P[k] = v
    return P

# ── 기준 (design.md §11 / WCAG 2.2 AA) ───────────────────────────
# 큰 텍스트 = 24px 이상, 또는 18.7px 이상 굵게 (18pt / 14pt bold)
TEXT, LARGE, GRAPHIC, EXEMPT = 4.5, 3.0, 3.0, 0.0
WHITE, BLACK = '#FFFFFFFF', '#FF000000'

# Windows11Palette 주입값.
# ⚠ 종전에는 여기에 hex 를 베껴 두고 "App.xaml.cs 와 동기화할 것"이라고만 적어 뒀다.
#    두 곳이 어긋나면 **감사는 통과하는데 화면은 미달**인 상태가 만들어진다.
#    이제 App.xaml.cs 와 이 스크립트가 **둘 다 Colors.xaml 의 Theme* 토큰을 읽는다.**
def palette(P):
    return {
        'PrimaryBorderColor':       P['ThemeInputBorderColor'],
        'PrimaryForegroundColor':   P['ThemePrimaryForegroundColor'],
        'SecondaryForegroundColor': P['ThemeSecondaryForegroundColor'],
        'PrimarySolidBorderColor':  P['ThemeSelectedColor'],
        'SelectedColor':            P['ThemeSelectedColor'],
        'AccentColor':              P['AppAccentColor'],
    }

def build(P):
    g = lambda k: P[k]
    PALETTE = palette(P)
    rows = [
        # ── 강조색 실사용 (Styles.xaml / Views) ────────────────────
        ('주요버튼 흰글자 / PrimaryActionBack',  'Styles.xaml:PrimaryActionBack', WHITE, g('AppAccentStrongColor'), TEXT, 1.0),
        ('위험버튼 글자(DangerText) / 흰 배경',   'Styles.xaml:DangerActionFore', g('DangerTextColor'), WHITE, TEXT, 1.0),
        ('핵심수치 30Bold 강조 / 흰 배경',        'Styles.xaml:109', g('AppAccentColor'), WHITE, LARGE, 1.0),
        ('상태문 14.5px AccentStrong / 흰',      'SessionBrowser:118', g('AppAccentStrongColor'), WHITE, TEXT, 1.0),
        ('아이콘 17~20px 강조 / 흰 배경',         'MainSurvey:57', g('AppAccentColor'), WHITE, GRAPHIC, 1.0),
        ('그룹헤더 3px 강조바 / 흰 배경',          'Styles.xaml:307', g('AppAccentColor'), WHITE, GRAPHIC, 1.0),

        # ── 셸 ────────────────────────────────────────────────────
        ('셸 보조텍스트 / 셸 배경',   'Colors.xaml', g('ShellForegroundColor'), g('ShellPrimaryColor'), TEXT, 1.0),
        ('셸 보조텍스트 / 셸 선택',   'Colors.xaml', g('ShellForegroundColor'), g('ShellSelectedColor'), TEXT, 1.0),
        ('셸 제목 / 셸 배경',        'Colors.xaml', g('ShellTitleForegroundColor'), g('ShellPrimaryColor'), TEXT, 1.0),


        # ── 값 고정(압정) : 고정된 칸 · 안내 띠 ────────────────────
        ('본문 글자 / 고정칸 배경',    'Colors.xaml', PALETTE['PrimaryForegroundColor'], g('PinnedInputBackColor'), TEXT, 1.0),
        ('고정 안내 글자 / 안내 배경',  'Colors.xaml', g('PinnedNoticeForeColor'), g('PinnedInputBackColor'), TEXT, 1.0),
        ('압정 아이콘 강조 / 고정칸',   'Colors.xaml', g('AppAccentColor'), g('PinnedInputBackColor'), GRAPHIC, 1.0),

        # ── 폼 필드 (관리자식 라벨칸) ──────────────────────────────
        ('폼라벨 글자 / 라벨칸 배경',  'Styles.xaml:197,198', g('FieldLabelForegroundBrush'), g('FieldLabelBackgroundBrush'), TEXT, 1.0),
        ('필수표시 * / 라벨칸 배경',   'Styles.xaml:201', g('RequiredMarkBrush'), g('FieldLabelBackgroundBrush'), TEXT, 1.0),
        ('필수표시 * / 흰 배경',       'Styles.xaml:201', g('RequiredMarkBrush'), WHITE, TEXT, 1.0),
        ('폼 구분선 / 흰 배경',        'Styles.xaml:200', g('FormLineBrush'), WHITE, GRAPHIC, 1.0),

        # ── Windows11Palette (App.xaml.cs 주입) ───────────────────
        ('입력 테두리 / 흰 배경',      'App.xaml.cs:PrimaryBorder', PALETTE['PrimaryBorderColor'], WHITE, GRAPHIC, 1.0),
        ('폼라벨·캡션 보조색 / 흰',    'App.xaml.cs:SecondaryFg', PALETTE['SecondaryForegroundColor'], WHITE, TEXT, 1.0),
        ('본문 글자 / 흰 배경',        'App.xaml.cs:PrimaryFg', PALETTE['PrimaryForegroundColor'], WHITE, TEXT, 1.0),
        ('본문 글자 / 얼룩행',         'App.xaml.cs:PrimaryFg', PALETTE['PrimaryForegroundColor'], P['RowAltColor'], TEXT, 1.0),
        ('본문 글자 / 읽기전용 배경',   'App.xaml.cs:PrimaryFg', PALETTE['PrimaryForegroundColor'], P['ReadOnlyBackColor'], TEXT, 1.0),
        ('선택 배경 / 흰 배경',        'App.xaml.cs:Selected', PALETTE['SelectedColor'], WHITE, EXEMPT, 1.0),

        # ── 표 ────────────────────────────────────────────────────
        # GridHeaderColor 는 현재 어느 화면에서도 참조되지 않는다(그리드 머리글은 Windows11 테마 기본값).
        # 쓰기 시작하면 EXEMPT 를 GRAPHIC 으로 바꾸고 하단 경계선을 같이 도입할 것.
        ('표머리글 배경(미사용 토큰) / 흰', 'Colors.xaml', g('GridHeaderColor'), WHITE, EXEMPT, 1.0),
        ('본문 검정 / 표머리글',       'Colors.xaml', BLACK, g('GridHeaderColor'), TEXT, 1.0),
        ('본문 검정 / 얼룩행',         'Colors.xaml', BLACK, g('RowAltColor'), TEXT, 1.0),
        ('본문 검정 / 2단행 아랫줄',   'Colors.xaml', BLACK, g('Row2LineColor'), TEXT, 1.0),
        ('본문 검정 / 읽기전용 배경',  'Colors.xaml', BLACK, g('ReadOnlyBackColor'), TEXT, 1.0),
        ('비활성 글자 / 비활성 배경',  'Colors.xaml', g('DisabledForeColor'), g('DisabledBackColor'), EXEMPT, 1.0),

        # ── 차트 ──────────────────────────────────────────────────
        ('차트 최대 / 흰 배경', 'Colors.xaml', g('MaxColor'), WHITE, GRAPHIC, 1.0),
        ('차트 평균 / 흰 배경', 'Colors.xaml', g('AvgColor'), WHITE, GRAPHIC, 1.0),
        ('차트 최소 / 흰 배경', 'Colors.xaml', g('MinColor'), WHITE, GRAPHIC, 1.0),
        # ══ 리디자인(단일 분류군 모드) 신규 토큰 실사용 ══════════════
        # design.md §3.7 : 색을 추가했으면 '실제로 얹히는 배경' 조합을 여기 등록한다.
        ('강조표면 배경(칩·방금추가행) / 흰',   'Colors.xaml:AccentSurface',     g('AccentSurfaceColor'),     WHITE, EXEMPT, 1.0),
        ('강조표면연 배경(선택 타일) / 흰',     'Colors.xaml:AccentSurfaceSoft', g('AccentSurfaceSoftColor'), WHITE, EXEMPT, 1.0),
        ('보호종 뱃지 글자 12px / 강조표면',    'FishEntry:보호종뱃지',   g('AccentOnSurfaceColor'), g('AccentSurfaceColor'), TEXT, 1.0),
        ('팝오버 현재 분류군 글자 / 강조표면',   'TaxonChipPopup',        g('AccentOnSurfaceColor'), g('AccentSurfaceColor'), TEXT, 1.0),
        ('방금추가 라벨 13px / 강조표면',       'FishEntry:방금추가',     g('AccentOnSurfaceColor'), g('AccentSurfaceColor'), TEXT, 1.0),
        ('접기·펼치기 링크 13px / 흰',         'CollapsibleSummary',     g('AccentOnSurfaceColor'), WHITE, TEXT, 1.0),
        ('위험표면 배경(교란종 뱃지) / 흰',     'Colors.xaml:DangerSurface',     g('DangerSurfaceColor'),     WHITE, EXEMPT, 1.0),
        ('위험표면연 배경(경고박스) / 흰',      'Colors.xaml:DangerSurfaceSoft', g('DangerSurfaceSoftColor'), WHITE, EXEMPT, 1.0),
        ('교란종 뱃지 글자 12px / 위험표면',    'FishEntry:교란종뱃지',   g('DangerOnSurfaceColor'), g('DangerSurfaceColor'),     TEXT, 1.0),
        ('경고박스 글자 13px / 위험표면연',     'HealthCard:경고박스',    g('DangerOnSurfaceColor'), g('DangerSurfaceSoftColor'), TEXT, 1.0),

        # 행 번호·워터마크 : 시안 1차본 #999(2.85:1) · #8a8a8a(3.5:1) 로 되돌아가지 말 것
        ('그리드 행번호 #666 / 흰',            'FishEntry:# 열',       g('TertiaryForegroundColor'), WHITE,                 TEXT, 1.0),
        ('그리드 행번호 #666 / 얼룩행',        'FishEntry:# 열',       g('TertiaryForegroundColor'), g('RowAltColor'),      TEXT, 1.0),
        ('합계행 라벨 #666 / 읽기전용 배경',    'FishEntry:합계행',      g('TertiaryForegroundColor'), g('ReadOnlyBackColor'), TEXT, 1.0),
        ('워터마크 #666 / 흰',                'QuickAddBar 워터마크',   g('TertiaryForegroundColor'), WHITE,                 TEXT, 1.0),
        ('타일 유형설명 #666 / 선택 타일',     'TaxonPicker 타일',      g('TertiaryForegroundColor'), g('AccentSurfaceSoftColor'), TEXT, 1.0),

        # 미입력 `·` 는 null ≠ 0 을 뜻하는 **의미 있는 문자**다. 가장 진한 보조색을 쓴다.
        ('미입력 `·` #444 / 흰',              'FishEntry:미입력', PALETTE['SecondaryForegroundColor'], WHITE,            TEXT, 1.0),
        ('미입력 `·` #444 / 얼룩행',          'FishEntry:미입력', PALETTE['SecondaryForegroundColor'], g('RowAltColor'), TEXT, 1.0),

        # ── 캡션 바 분류군 칩 (종전 '야외 모드' 버튼 자리) ────────────
        ('칩 열림 배경 / 셸 배경',            'Colors.xaml:ShellChipBack', g('ShellChipBackColor'),      g('ShellPrimaryColor'),  EXEMPT,  1.0),
        ('칩 분류군명 13.5px / 칩 배경',       'MainSurvey:칩',   g('ShellTitleForegroundColor'), g('ShellChipBackColor'), TEXT,    1.0),
        ('칩 보조문구 12.5px / 칩 배경',       'MainSurvey:칩',   g('ShellForegroundColor'),      g('ShellChipBackColor'), TEXT,    1.0),
        ('칩 2px 강조 테두리 / 칩 배경',       'MainSurvey:칩',   g('AppAccentColor'),            g('ShellChipBackColor'), GRAPHIC, 1.0),
        ('세션 요약 13px / 셸 배경',          'MainSurvey:세션요약', g('ShellMutedForegroundColor'), g('ShellPrimaryColor'), TEXT,   1.0),
        ('셸 pill 테두리 / 셸 배경',          'MainSurvey:자료함·대기', g('ShellPillBorderColor'), g('ShellPrimaryColor'), GRAPHIC, 1.0),
        ('셸 구분선 / 셸 배경',               'MainSurvey:구분선',  g('ShellDividerColor'),     g('ShellPrimaryColor'),  EXEMPT,  1.0),

        # ── 면(surface) : 흰 카드 + 옅은 테두리 (시안 실측) ──────────
        # ⚠ 카드 경계는 '장식 구분선'이라 3:1 이 면제된다(§3.7).
        #    입력 요소 경계(App.xaml.cs PrimaryBorderColor · 3.36:1)와 혼동하지 말 것 —
        #    카드가 회색으로 읽힌다고 입력칸 테두리를 옅게 만들면 1.4.11 위반이 된다.
        ('본문 바닥 / 흰 카드',               'Colors.xaml:PageBack',    g('PageBackColor'),   WHITE, EXEMPT, 1.0),
        ('카드 테두리 / 흰 배경',             'Colors.xaml:CardBorder',  g('CardBorderColor'), WHITE, EXEMPT, 1.0),
        ('카드 구분선 / 흰 배경',             'Colors.xaml:CardDivider', g('CardDividerColor'),WHITE, EXEMPT, 1.0),
        ('본문 검정 / 카드 흰 면',            'Styles.xaml:CardBorder',  BLACK, g('CardBackColor'), TEXT, 1.0),
        ('본문 검정 / 본문 바닥',             'Colors.xaml:PageBack',    BLACK, g('PageBackColor'), TEXT, 1.0),
        ('검색창 강조 테두리 / 흰 배경',       'QuickAddBar:SearchBox',   g('AppAccentColor'),  WHITE, GRAPHIC, 1.0),

        # ── 주요 버튼 : 상태마다 배경이 바뀌므로 **상태별로** 등록한다 ──
        # ⚠ 종전에는 기본 상태만 봤다. 마우스오버에서 테마가 배경을 덮어써 흰 글자가 사라졌는데
        #    감사에는 잡히지 않았다(감사가 보지 않는 상태였다).
        ('주요버튼 흰 글자 / 기본',      'PrimaryActionButtonStyle', WHITE, g('AppAccentStrongColor'),        TEXT, 1.0),
        ('주요버튼 흰 글자 / 마우스오버', 'PrimaryActionButtonStyle', WHITE, g('PrimaryActionHoverColor'),    TEXT, 1.0),
        ('주요버튼 흰 글자 / 눌림',      'PrimaryActionButtonStyle', WHITE, g('PrimaryActionPressedColor'),  TEXT, 1.0),
        ('돋보기 아이콘 / 흰 배경',           'QuickAddBar:Magnify',     g('AccentOnSurfaceColor'), WHITE, GRAPHIC, 1.0),

        # ── 포커스 표시 · 행 강조바 (§11.1 — 어떤 사유로도 제거하지 않는다) ──
        ('포커스링 2px / 흰 배경',            'design.md §11.1', g('AppAccentColor'), WHITE,            GRAPHIC, 1.0),
        ('포커스링 2px / 얼룩행',             'design.md §11.1', g('AppAccentColor'), g('RowAltColor'), GRAPHIC, 1.0),
        ('포커스링 2px / 읽기전용 배경',       'design.md §11.1', g('AppAccentColor'), g('ReadOnlyBackColor'), GRAPHIC, 1.0),
        ('포커스링·편집셀 2px / 방금추가 행',   'FishEntry:편집셀', g('AppAccentColor'), g('AccentSurfaceColor'), GRAPHIC, 1.0),
        # ⚠ 채워진 강조색 단추 위의 포커스 링. 종전 감사는 '흰 배경 위 링'만 봤고
        #    **링을 강조색 단추 위에 그리는 경우**가 빠져 있었다 — 실제로 1.24:1 이었다(2026-08-31 실기기 확인).
        #    흰 링을 안쪽으로 들여 그리도록 고쳤으므로 3상태 모두 검사한다.
        ('기본단추 포커스링 2px / 기본단추 배경',   'PrimaryActionButtonStyle', WHITE, g('AppAccentStrongColor'),      GRAPHIC, 1.0),
        ('기본단추 포커스링 2px / 마우스오버 배경', 'PrimaryActionButtonStyle', WHITE, g('PrimaryActionHoverColor'),   GRAPHIC, 1.0),
        ('기본단추 포커스링 2px / 눌림 배경',       'PrimaryActionButtonStyle', WHITE, g('PrimaryActionPressedColor'), GRAPHIC, 1.0),
        # 캡션 바(어두운 셸) 위의 포커스 링 — 분류군 칩 · 자료함/동기화 pill
        ('포커스링 2px / 셸 배경',   'TaxonChipToggleStyle', g('AppAccentColor'), g('ShellPrimaryColor'),  GRAPHIC, 1.0),
        ('포커스링 2px / 칩 배경',   'ShellPillButtonStyle', g('AppAccentColor'), g('ShellChipBackColor'), GRAPHIC, 1.0),
        ('보호종 3px 바 / 흰 배경',           'FishEntry:보호종', g('AppAccentStrongColor'), WHITE, GRAPHIC, 1.0),
        ('교란종 3px 바 / 흰 배경',           'FishEntry:교란종', g('DangerTextColor'),      WHITE, GRAPHIC, 1.0),

        # ── 건강성 카드 M1~M8 막대 ──────────────────────────────────
        # 낮은 항목은 색을 달리하되 이름(M4·M8)도 함께 적는다 — 색만으로 구분하지 않는다(§11).
        # ⚠ 시안은 낮은 항목에 GradeC(#F9A825)를 썼지만 흰 배경 1.97:1 로 미달이다.
        #    한 단계 진한 GradeD 로 바꿨다(3.08:1).
        ('M1~M8 막대(보통) / 흰 배경',        'HealthCard:막대', g('AppAccentColor'), WHITE, GRAPHIC, 1.0),
        ('M1~M8 막대(낮은 항목) / 흰 배경',    'HealthCard:막대', g('GradeDColor'),    WHITE, GRAPHIC, 1.0),
    ]

    # ── 등급 뱃지 : GradeBadgeTextStyle 은 FontSize 를 지정하지 않는다
    #    → 주변 폰트(DefaultFontSize 14.5)를 상속하므로 '일반 텍스트' 기준이다.
    #    A·E 는 흰 글자, B·C·D·미평가는 GradeBadgeDarkFore (Styles.xaml GradeBadgeTextStyle 트리거)
    DARK = P.get('GradeBadgeDarkForeColor', BLACK)
    for g_, key, fore in [('A','GradeAColor',WHITE), ('B','GradeBColor',DARK),
                          ('C','GradeCColor',DARK),  ('D','GradeDColor',DARK),
                          ('E','GradeEColor',WHITE), ('-','GradeNoneColor',DARK)]:
        tone = '흰글자' if fore == WHITE else '검은글자'
        rows.append((f'등급 {g_} 뱃지 {tone} 14.5px', 'Styles.xaml:GradeBadgeText', fore, P[key], TEXT, 1.0))

    # ── 분류군 색점 : 9x9, 미선택 시 Opacity 0.4 ─────────────────
    # ── 분류군 색점 ─────────────────────────────────────────────
    #  · 흰 배경(팝오버 항목 · 첫 실행 타일 · 본문)  → 기본 7색
    #  · 딥네이비 셸(캡션 바) · 칩 열림 배경         → *OnShell 변형(§3.8 계열)
    #    기본 7색은 흰 배경 기준 보정값이라 셸 위에서 1.36~3.95:1 로 무너진다.
    for name, key in [('어류','Fish'),('저서동물','Benthos'),('조류','Bird'),
                      ('포유류','Mammal'),('양서파충류','AmphibianReptile'),
                      ('서식수변','HabitatWaterEdge'),('수질','WaterQuality')]:
        base  = P[key + 'Color']
        shell = P[key + 'OnShellColor']
        rows.append((f'색점 {name} / 흰 배경',        'TaxonPicker·Popup dot', base,  WHITE,                     GRAPHIC, 1.0))
        rows.append((f'색점 {name} / 선택 타일',      'TaxonPicker dot',       base,  P['AccentSurfaceSoftColor'], GRAPHIC, 1.0))
        rows.append((f'색점 {name} / 현재 항목 배경',  'TaxonChipPopup dot',    base,  P['AccentSurfaceColor'],     GRAPHIC, 1.0))
        rows.append((f'색점 {name} / 셸 배경',        'MainSurvey 칩 dot',     shell, P['ShellPrimaryColor'],      GRAPHIC, 1.0))
        rows.append((f'색점 {name} / 칩 열림 배경',    'MainSurvey 칩 dot',     shell, P['ShellChipBackColor'],     GRAPHIC, 1.0))
    return rows

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--verbose', '-v', action='store_true')
    a = ap.parse_args()

    P = load_colors(COLORS, STYLES)
    if not P:
        print('색 키를 찾지 못했습니다. 경로를 확인하세요:', COLORS); return 2

    rows = build(P)
    fails = []
    print(f'■ SDSM_Surveyor 대비 감사 — 색 키 {len(P)}개 · 검사 {len(rows)}건')
    print(f"{'조합':<38}{'대비':>9}{'기준':>7}  판정  위치")
    print('-' * 92)
    for name, where, fg, bg, req, op in rows:
        r = contrast(fg, bg, op)
        if req == EXEMPT:      v = 'EXEMPT'
        elif r >= req:         v = 'PASS'
        else:                  v = 'FAIL'; fails.append((name, r, req, where))
        if a.verbose or v == 'FAIL':
            print(f'{name:<38}{r:>8.2f}:1{req:>7.1f}  {v:<6} {where}')
    print('-' * 92)
    print(f'총 {len(rows)}건 · 미달 {len(fails)}건')
    if fails:
        print('\n[미달 목록]')
        for n, r, q, w in fails:
            print(f'  ✗ {n}: {r:.2f}:1  (기준 {q})  ← {w}')
    return 1 if fails else 0

if __name__ == '__main__':
    sys.exit(main())
