#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
제출 엑셀의 `조사지점` 시트 → 지점 마스터 `sites.json` 생성 (04_FEATURE_SITE_SESSION §A-2).

⚠ **파일마다 `조사지점` 시트의 열 구성이 다르다.** (하천별·분류군별로 제각각)
  - 곡교천 : 지점설명 | DB상 지점명 | 위도(DMS) | 경도(DMS) | 22~25 | 26 | (St번호) | 비고
  - 오산천 : 조사지점 | 지점명 | 하천차수 | 위도(십진) | 경도(십진) | 연도별 지도번호…
  - 원천리천: 지점 | DB 지점명 | 위치 | 위도 | 경도 | 연도별 표기…
  그래서 **열 번호를 고정하지 않고 행2의 헤더 텍스트로 위치를 찾는다.**

사용법:
    python tools/build_sites_json.py
    python tools/build_sites_json.py --input <폴더> --output <sites.json> --version sites-20260820

생태현황 지점(2026-10-18 결정 D4) — 방류하천 항목은 그대로 두고 생태현황만 다시 만든다:
    python tools/build_sites_json.py --ecostatus ../SDSM/docs/_ecostatus_sites_extracted.md
        --version sites-20261018 --csv tools/ecostatus_sites_20261018.csv      (한 줄로 친다)
"""
from __future__ import annotations

import argparse
import datetime as _dt
import glob
import io
import json
import os
import re
import sys

try:
    import openpyxl
except ImportError:  # pragma: no cover
    sys.exit("openpyxl 이 필요합니다:  pip install openpyxl")

SHEET = "조사지점"
HEADER_ROW = 2      # 1-based. 데이터는 행3부터
DATA_ROW = 3

# 관리자 DB `SiteDivision` 기준 (하천 → 사업장)
WORKPLACE_BY_RIVER = {"오산천": "기흥", "원천리천": "화성", "곡교천": "온양"}

# 곡교천은 한 과업대표하천 안에 지점 그룹이 여러 개다(SiteDivision.WorkplaceSite)
SITE_GROUPS = {"곡교천": ["곡교천", "천안천", "회룡천", "공수리"]}

TAXA = ["어류", "저서동물", "조류", "포유류", "양서파충류", "수질", "서식수변"]

_YEAR_HEADER = re.compile(r"^\s*'?\d{2,4}\s*(~\s*\d{2,4})?\s*$")
_ST_VALUE = re.compile(r"^\s*st\.?\s*\d+", re.IGNORECASE)
_DMS = re.compile(r"""^\s*(\d+)\s*°\s*(\d+)\s*'\s*([\d.]+)\s*"?\s*([NSEW])?\s*$""")


def s(v) -> str | None:
    if v is None:
        return None
    t = str(v).strip()
    return t or None


def to_decimal(v) -> float | None:
    """위/경도를 십진수로. 엑셀에 DMS(36°45'47.09"N)와 십진수가 섞여 있다."""
    t = s(v)
    if t is None:
        return None
    m = _DMS.match(t)
    if m:
        deg, minute, sec, hemi = m.groups()
        val = int(deg) + int(minute) / 60 + float(sec) / 3600
        if hemi and hemi.upper() in ("S", "W"):
            val = -val
        return round(val, 6)
    try:
        return round(float(t), 6)
    except ValueError:
        return None


def parse_filename(path: str) -> tuple[str | None, str | None, str | None]:
    """파일명 → (대분류, 과업대표하천, 분류군). 예: 2602_방류하천_곡교천_어류_DB_v1.xlsx"""
    name = os.path.basename(path)
    project = "방류하천" if "방류하천" in name else ("생태현황" if "생태현황" in name else None)
    river = next((r for r in WORKPLACE_BY_RIVER if r in name), None)
    taxon = next((t for t in TAXA if t in name), None)
    return project, river, taxon


def find_columns(header: list) -> dict:
    """행2 헤더 텍스트로 열 위치를 찾는다(파일마다 구성이 달라 고정할 수 없다)."""
    cols: dict = {"years": {}}
    for i, raw in enumerate(header):
        h = s(raw)
        if not h:
            continue
        if "지점명" in h:
            cols.setdefault("name", i)
        elif h in ("지점설명", "조사지점", "지점"):
            cols.setdefault("desc", i)
        elif h in ("위치",):
            cols.setdefault("desc2", i)
        elif h == "위도":
            cols["lat"] = i
        elif h == "경도":
            cols["lng"] = i
        elif "하천차수" in h:
            cols["chasu"] = i
        elif h == "비고":
            cols["note"] = i
        elif _YEAR_HEADER.match(h):
            cols["years"][h.strip().lstrip("'")] = i
    # 지점명 헤더가 없으면 관례상 C열
    cols.setdefault("name", 2)
    cols.setdefault("desc", 1)
    return cols


def find_st_column(ws, cols: dict) -> int | None:
    """`St.1` 형태의 조사장소 번호 열은 헤더가 비어 있는 경우가 있어 값으로 찾는다."""
    counts: dict[int, int] = {}
    for row in ws.iter_rows(min_row=DATA_ROW, values_only=True):
        # 데이터 블록(연속 구간) 안에서만 찾는다 — 아래쪽 메모의 "※ st.3 …"에 걸리지 않도록
        head = s(row[cols["name"]]) if cols["name"] < len(row) else None
        if not head:
            break
        for i, v in enumerate(row):
            t = s(v)
            if t and _ST_VALUE.match(t):
                counts[i] = counts.get(i, 0) + 1
    if not counts:
        return None
    return max(counts, key=lambda k: counts[k])


def normalize_st(v: str) -> str:
    """`st.3` `ST 3` → `St.3` 로 표기 통일."""
    m = re.search(r"\d+", v)
    return f"St.{m.group()}" if m else v


def site_group(river: str | None, name: str) -> str | None:
    """곡교천처럼 한 하천에 지점 그룹이 여럿이면 지점명 접두어로 판별(SiteDivision.WorkplaceSite)."""
    if not river:
        return None
    for g in SITE_GROUPS.get(river, [river]):
        if name.startswith(g):
            return g
    return river


# ── 생태현황 지점 (2026-10-18 결정 D4 ①) ──────────────────────────────────────────────
# 원천: ..\SDSM\docs\_ecostatus_sites_extracted.md
#   §3 추출 목록 149개(어류 118 · 조류 28 · 포유류 3) = 등록 시드(§9 · 2026-09-06 판정)
#   §4(가) 표 = '여러 값' 지점의 기록별 좌표 · §8-2 Project 로만 구분(개명 없음) · §8-3 연도별 별개 지점
# 방류하천 지점은 건드리지 않는다 — 기존 sites.json 의 방류하천 항목을 그대로 옮겨 쓴다.
# 이상 항목(§4·§7)은 등록을 막지 않고 Desc 에 '보완 필요: …' 로 적는다(값은 조사자료 그대로 · §9).

ECO = "생태현황"
SAME_PLACE_M = 300          # §7-1 : 300m 이내 = 같은 장소
ECO_TAXA_ORDER = {"어류": 0, "조류": 1, "포유류": 2}
# §4(라) : 하천명 칸에 하천이 아닌 값(어류 0 · 호소·공원 / 조류 현장 메모·지명·시설)
NON_RIVER = {"0", "배다리생태공원", "신정저수지", "송악저수지", "도고저수지", "아산호", "삽교호",
             "하천변 제방공사중", "진위천 합류부", "동탄 여울공원", "기흥호수", "설화산"}
REGION_NAMES = {"천안", "평택", "온양", "용인산단"}   # §4(라)·(사) : 권역명
UNDECIDED_PAIRS = [("황구지천", "황구지천1")]           # §8-5 #4 미판정


def _md_section(text: str, head: str) -> list[str]:
    """`### 3-1.` 처럼 시작하는 절의 줄들(다음 같은 수준 머리글 전까지)."""
    lines = text.splitlines()
    start = next(i for i, l in enumerate(lines) if l.startswith(head))
    level = len(head) - len(head.lstrip("#"))
    out = []
    for l in lines[start + 1:]:
        if l.startswith("#") and len(l) - len(l.lstrip("#")) <= level:
            break
        out.append(l)
    return out


def _md_rows(lines: list[str]) -> list[list[str]]:
    """마크다운 표 본문 행(머리글·구분선 제외)."""
    rows, seen_sep = [], False
    for l in lines:
        if not l.startswith("|"):
            if seen_sep and rows:
                break
            continue
        cells = [c.strip() for c in l.strip().strip("|").split("|")]
        if all(re.fullmatch(r":?-+:?", c) for c in cells if c):
            seen_sep = True
            continue
        if seen_sep:
            rows.append(cells)
    return rows


def _num(t: str) -> float | None:
    t = t.replace("*", "").replace(",", "").strip()
    try:
        return float(t)
    except ValueError:
        return None


def _year(yc: str) -> int | None:
    m = re.match(r"\s*(\d{4})", yc or "")
    return int(m.group(1)) if m else None


def _dist_m(a: tuple, b: tuple) -> float:
    import math
    la1, lo1, la2, lo2 = map(math.radians, (a[0], a[1], b[0], b[1]))
    h = math.sin((la2 - la1) / 2) ** 2 + math.cos(la1) * math.cos(la2) * math.sin((lo2 - lo1) / 2) ** 2
    return 6371008.8 * 2 * math.asin(math.sqrt(h))


def _counts(cell: str) -> list[tuple[str, int]]:
    """`천안(139) / 평택(35)` → [(천안,139),(평택,35)] — 기록 수 많은 순."""
    out = []
    for part in cell.split("/"):
        m = re.match(r"\s*(.+?)\s*\((\d+)\)\s*$", part)
        if m:
            out.append((m.group(1), int(m.group(2))))
        elif part.strip():
            out.append((part.strip(), 0))
    return sorted(out, key=lambda x: -x[1])


def _group_of(name: str) -> str | None:
    """조사권역(SiteGroup) — 지점명 접두어(§3-1 : 접두어는 하천이 아니라 조사권역). `1지점` 류는 없음."""
    if re.fullmatch(r"\d+지점", name):
        return None
    g = re.sub(r"\d+$", "", name)
    return g or None


def _river_flags(values: list[tuple[str, int]]) -> list[str]:
    flags = []
    if len(values) > 1:
        flags.append("하천명 여러 개(" + " · ".join(f"{v} {n}건" for v, n in values) + ")")
    main = values[0][0] if values else None
    if main in NON_RIVER or main in REGION_NAMES or (main or "").isdigit():
        flags.append(f"하천명(원본 값 '{main}')")
    return flags


def _eco_site(taxon, name, river, lat, lng, chasu, flags, vfrom=None, vto=None) -> dict:
    rec = {
        "Project": ECO,
        "Workplace": None,                 # 관리자 DB SiteDivision(생태현황)의 사업장이 비어 있다
        "River": river,
        "SiteGroup": _group_of(name),
        "SiteName": name,
        "StNo": None,
        "Desc": ("보완 필요: " + " · ".join(flags)) if flags else None,
        "Chasu": chasu,
        "Lat": lat,
        "Lng": lng,
        "YearAliases": {},
        "MapNumbers": {},
        "Taxa": [taxon],
    }
    if vfrom is not None or vto is not None:
        rec["ValidFrom"] = vfrom
        rec["ValidTo"] = vto
    return rec


def build_ecostatus(md_path: str) -> tuple[list[dict], list[str]]:
    """§3 목록 → 생태현황 지점. (지점들, 판정 기록) 를 돌려준다."""
    text = io.open(md_path, encoding="utf-8-sig").read()
    log: list[str] = []
    sites: list[dict] = []

    # 어류 ─ §3-1 목록 + §4(가) 기록별 좌표
    fish = _md_rows(_md_section(text, "### 3-1."))
    multi: dict[str, list[dict]] = {}
    cur = None
    for r in _md_rows(_md_section(text, "### (가)")):
        name, yc, river, lat, lng, chasu = r[0], r[1], r[2], r[3], r[4], r[5]
        cur = name or cur
        multi.setdefault(cur, []).append({"year": _year(yc), "yc": yc, "river": river,
                                          "lat": _num(lat), "lng": _num(lng), "chasu": int(_num(chasu))})
    undecided = {n for pair in UNDECIDED_PAIRS for n in pair}
    for r in fish:
        name, river, lat, lng, chasu, yc = r[0], r[1], r[2], r[3], r[4], r[5]
        if lat == "여러 값":
            recs = multi[name]
        else:
            recs = [{"year": _year(yc), "yc": yc, "river": river, "lat": _num(lat), "lng": _num(lng),
                     "chasu": int(_num(chasu)) if _num(chasu) is not None else None}]
        base_flags = [f"{'·'.join(p)} 같은 지점인지 미판정" for p in UNDECIDED_PAIRS if name in p] if name in undecided else []
        years = sorted({x["year"] for x in recs})
        far = any(_dist_m((a["lat"], a["lng"]), (b["lat"], b["lng"])) > SAME_PLACE_M
                  for a in recs for b in recs if a["year"] != b["year"])
        if len(years) > 1 and far:
            # §8-3 연도별 별개 지점 — 시작 연도 ~ 다음 조사 연도 직전
            for i, y in enumerate(years):
                vto = years[i + 1] - 1 if i + 1 < len(years) else None
                same = [x for x in recs if x["year"] == y]
                flags = list(base_flags)
                pick = next((x for x in same if x["river"] == _group_of(name)), same[0])
                for o in same:
                    if o is not pick and _dist_m((o["lat"], o["lng"]), (pick["lat"], pick["lng"])) > SAME_PLACE_M:
                        flags.append(f"{y}년 같은 차수에 {o['river']}({o['lat']:.6f}, {o['lng']:.6f})도 이 이름으로 조사됨")
                sites.append(_eco_site("어류", name, pick["river"], pick["lat"], pick["lng"], pick["chasu"], flags, y, vto))
                log.append(f"  어류 {name} [{y}~{vto or ''}] {pick['river']} {pick['lat']:.6f},{pick['lng']:.6f} 차수 {pick['chasu']}"
                           + (f"  ← 같은 해 {len(same)}건 중 하천명=조사권역({_group_of(name)}) 우선" if len(same) > 1 else ""))
        else:
            # 같은 장소(300m 이내) — 가장 최근 조사 기록의 좌표·차수(§9 '25년까지 내용으로 기초틀')
            pick = sorted(recs, key=lambda x: x["year"] or 0)[-1]
            flags = list(base_flags)
            rivers = sorted({x["river"] for x in recs})
            if len(rivers) > 1:
                flags.append("하천명 여러 개(" + " · ".join(rivers) + ")")
            if pick["river"] in NON_RIVER or (pick["river"] or "").isdigit():
                flags.append(f"하천명(원본 값 '{pick['river']}')")
            chs = sorted({(x["year"], x["chasu"]) for x in recs})
            if len({c for _, c in chs}) > 1:
                flags.append("하천차수(" + " · ".join(f"{y}년 {c}" for y, c in chs) + ")")
            if len(recs) > 1:
                log.append(f"  어류 {name} 기록 {len(recs)}건 같은 장소(최대 {max(_dist_m((a['lat'], a['lng']), (b['lat'], b['lng'])) for a in recs for b in recs):.0f}m) → {pick['year']}년 기록 좌표")
            sites.append(_eco_site("어류", name, pick["river"], pick["lat"], pick["lng"], pick["chasu"], flags))

    # 조류 ─ §3-2 : 좌표 없음(조사 기록의 좌표는 정점 좌표가 아니다 · §7-5·§7-6 · §9 (가))
    for r in _md_rows(_md_section(text, "### 3-2.")):
        values = _counts(r[1])
        sites.append(_eco_site("조류", r[0], values[0][0] if values else None, None, None, None,
                               ["좌표"] + _river_flags(values)))
    # 포유류 ─ §3-3 : 권역 3개 · 좌표 없음(선형 조사 궤적 · 좌표 오류 3건 §7-3·§7-4)
    for r in _md_rows(_md_section(text, "### 3-3.")):
        values = _counts(r[1])
        sites.append(_eco_site("포유류", r[0], values[0][0] if values else None, None, None, None,
                               ["좌표"] + _river_flags(values)))

    sites.sort(key=lambda s: (ECO_TAXA_ORDER.get(s["Taxa"][0], 9), _natural(s["SiteName"]), s.get("ValidFrom") or 0))
    return sites, log


def _natural(s: str) -> list:
    return [(0, int(t), "") if t.isdigit() else (1, 0, t) for t in re.findall(r"\d+|\D+", s)]


def write_ecostatus(args) -> int:
    """기존 sites.json 의 방류하천 항목은 그대로 두고 생태현황 항목만 다시 만든다."""
    with io.open(args.output, encoding="utf-8-sig") as f:
        cur = json.load(f)
    keep = [s for s in cur["Sites"] if s.get("Project") != ECO]
    eco, log = build_ecostatus(args.ecostatus)
    names = {(s["Taxa"][0], s["SiteName"]) for s in eco}
    by_taxon = {t: len({n for tt, n in names if tt == t}) for t in ECO_TAXA_ORDER}
    data = {
        "Version": args.version or f"sites-{_dt.date.today():%Y%m%d}",
        "GeneratedAt": _dt.datetime.now().replace(microsecond=0).isoformat(),
        "Sites": keep + eco,
    }
    with io.open(args.output, "w", encoding="utf-8", newline="\n") as f:
        json.dump(data, f, ensure_ascii=False, indent=2)
        f.write("\n")

    if args.csv:
        import csv
        cols = ["Project", "Taxa", "SiteName", "ValidFrom", "ValidTo", "River", "SiteGroup", "Lat", "Lng", "Chasu", "Desc"]
        with io.open(args.csv, "w", encoding="utf-8-sig", newline="") as f:
            w = csv.writer(f, lineterminator="\n")   # 저장소 개행 LF(.gitattributes)
            w.writerow(cols)
            for s in eco:
                w.writerow([",".join(s["Taxa"]) if c == "Taxa" else ("" if s.get(c) is None else s.get(c)) for c in cols])

    print(f"결과 : {args.output}")
    print(f"  Version    {data['Version']}")
    print(f"  방류하천    {len(keep)}개 (그대로)")
    print(f"  생태현황    지점명 {len(names)}개 · 항목 {len(eco)}개 (연도 구간으로 나뉜 지점 포함)")
    for t, n in by_taxon.items():
        print(f"    {t:<4} {n:>3}개")
    flagged = [s for s in eco if s["Desc"]]
    print(f"  보완 필요   {len(flagged)}개")
    print("판정 기록")
    for line in log:
        print(line)
    if args.csv:
        print(f"CSV : {args.csv}")
    return 0


def main() -> int:
    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    ap = argparse.ArgumentParser(description="제출 엑셀 조사지점 시트 → sites.json")
    ap.add_argument("--input", default=r"F:\17_한국환경지리연구소\2_삼성DS\2026년\5_보낸파일\20260305\일괄입력자료")
    ap.add_argument("--output", default=os.path.join(root, "SDSM_Surveyor_App", "sites.json"))
    ap.add_argument("--version", default=None, help="기본값: sites-YYYYMMDD")
    ap.add_argument("--ecostatus", default=None, help="생태현황 추출 보고서(_ecostatus_sites_extracted.md) — 주면 생태현황 항목만 다시 만든다")
    ap.add_argument("--csv", default=None, help="--ecostatus 와 함께 : 생태현황 목록 CSV")
    args = ap.parse_args()
    if args.ecostatus:
        return write_ecostatus(args)

    files = sorted(glob.glob(os.path.join(args.input, "*.xlsx")))
    if not files:
        return int(bool(sys.stderr.write(f"입력 폴더에 xlsx가 없습니다: {args.input}\n")))

    merged: dict[tuple, dict] = {}
    skipped: list[str] = []
    per_file: list[str] = []

    for path in files:
        project, river, taxon = parse_filename(path)
        wb = openpyxl.load_workbook(path, read_only=True, data_only=True)
        try:
            if SHEET not in wb.sheetnames:
                skipped.append(f"{os.path.basename(path)} — '{SHEET}' 시트 없음")
                continue
            ws = wb[SHEET]
            rows = list(ws.iter_rows(min_row=1, max_row=HEADER_ROW, values_only=True))
            if len(rows) < HEADER_ROW:
                skipped.append(f"{os.path.basename(path)} — 헤더 행 없음")
                continue
            cols = find_columns(list(rows[HEADER_ROW - 1]))
            st_col = find_st_column(ws, cols)

            added = 0
            for row in ws.iter_rows(min_row=DATA_ROW, values_only=True):
                def at(idx):
                    return s(row[idx]) if idx is not None and idx < len(row) else None

                name = at(cols["name"])
                # ⚠ 지점 표는 행3부터 **연속**이고, 빈 행 아래에는 범례·메모가 섞여 있다
                #   (예: "2011", "2024 조사지점", "※ st.3 대황교 지점 잘못 설명됨").
                #   그래서 첫 빈 지점명에서 멈춘다. 계속 훑으면 메모가 지점으로 들어온다.
                if not name:
                    break
                if name in ("지점명", "DB상 지점명", "DB 지점명"):
                    continue

                key = (project, river, name)
                rec = merged.setdefault(key, {
                    "Project": project,
                    "Workplace": WORKPLACE_BY_RIVER.get(river or ""),
                    "River": river,
                    "SiteGroup": site_group(river, name),
                    "SiteName": name,
                    "StNo": None,
                    "Desc": None,
                    "Chasu": None,
                    "Lat": None,
                    "Lng": None,
                    "YearAliases": {},
                    "MapNumbers": {},
                    "Taxa": [],
                })

                # 원천리천은 '지점' 열이 번호(0,1,2…)라 설명이 못 된다 → '위치' 열을 우선
                d1, d2 = at(cols.get("desc")), at(cols.get("desc2"))
                if d1 and d1.replace(".", "").isdigit():
                    d1, d2 = d2, d1
                rec["Desc"] = rec["Desc"] or d1 or d2
                rec["Lat"] = rec["Lat"] if rec["Lat"] is not None else to_decimal(row[cols["lat"]] if "lat" in cols and cols["lat"] < len(row) else None)
                rec["Lng"] = rec["Lng"] if rec["Lng"] is not None else to_decimal(row[cols["lng"]] if "lng" in cols and cols["lng"] < len(row) else None)
                if rec["Chasu"] is None and "chasu" in cols:
                    c = at(cols["chasu"])
                    if c and c.isdigit():
                        rec["Chasu"] = int(c)
                if rec["StNo"] is None and st_col is not None:
                    v = at(st_col)
                    if v and _ST_VALUE.match(v):
                        rec["StNo"] = normalize_st(v)
                # 연도별 열에는 두 종류가 섞여 있다 : 그 시기의 **지점명**(오산천5)과 **지도 번호**(3).
                # 의미가 다르므로 분리해 담는다.
                for label, idx in cols["years"].items():
                    v = at(idx)
                    if not v:
                        continue
                    if v.isdigit():
                        rec["MapNumbers"].setdefault(label, v)
                    elif _ST_VALUE.match(v):
                        rec["StNo"] = rec["StNo"] or normalize_st(v)
                    else:
                        rec["YearAliases"].setdefault(label, v)

                note = at(cols.get("note"))
                if note and "수질만" in note.replace(" ", ""):
                    rec["Taxa"] = ["수질"]          # 비고가 명시하면 그것이 우선
                elif taxon and taxon not in rec["Taxa"] and rec["Taxa"] != ["수질"]:
                    rec["Taxa"].append(taxon)
                added += 1
            per_file.append(f"  {os.path.basename(path):<44} {added:>3}행  열={{name:{cols['name']}, lat:{cols.get('lat')}, lng:{cols.get('lng')}, st:{st_col}}}")
        finally:
            wb.close()

    sites = sorted(merged.values(), key=lambda r: (r["Project"] or "", r["River"] or "", r["SiteName"]))
    version = args.version or f"sites-{_dt.date.today():%Y%m%d}"
    data = {
        "Version": version,
        "GeneratedAt": _dt.datetime.now().replace(microsecond=0).isoformat(),
        "Sites": sites,
    }
    with io.open(args.output, "w", encoding="utf-8", newline="\n") as f:
        json.dump(data, f, ensure_ascii=False, indent=2)
        f.write("\n")

    print("파일별 처리")
    for line in per_file:
        print(line)
    if skipped:
        print(f"\n건너뛴 파일 ({len(skipped)})")
        for x in skipped:
            print(f"  {x}")

    no_coord = [x["SiteName"] for x in sites if x["Lat"] is None or x["Lng"] is None]
    no_st = [x["SiteName"] for x in sites if not x["StNo"]]
    print(f"\n결과 : {args.output}")
    print(f"  Version   {version}")
    print(f"  지점 수    {len(sites)}")
    for river in sorted({(x['River'] or '(없음)') for x in sites}):
        n = sum(1 for x in sites if (x["River"] or "(없음)") == river)
        print(f"    {river:<8} {n:>3}")
    print(f"  좌표 없음  {len(no_coord)}건" + (f" : {', '.join(no_coord[:10])}" if no_coord else ""))
    print(f"  St번호 없음 {len(no_st)}건" + (f" : {', '.join(no_st[:10])}…" if no_st else ""))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
