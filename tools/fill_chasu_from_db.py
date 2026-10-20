#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
방류하천 지점의 빈 하천차수(`Chasu`)를 게시 DB 어류 기록에서 찾는다(2026-10-20 결정 · 문제 7·10).

  - DB 는 **읽기 전용**(`mode=ro`)으로 연다. DB·관리자 저장소에는 아무것도 쓰지 않는다.
  - 후보 = 그 지점 이름의 어류 기록 + 지점명 이력(`YearAliases`)의 옛 이름으로 그 해에 남은 기록의 `AutoRiverChasu`.
  - **한 값으로 일치하고 지점명이 헷갈리지 않는 곳만** 채운다. 기록이 없거나, 값이 어긋나거나,
    옛 이름이 다른 지점 이름과 겹치는 곳은 비워 두고 판정표에 적는다.
  - 생태현황 항목과 다른 칸은 건드리지 않는다. 이미 하천차수가 있는 지점도 그대로.

사용법(판정표만):
    python tools/fill_chasu_from_db.py --db <SDSMDB.sqlite> --csv tools/chasu_fill_20261020.csv
sites.json 에 채우기:
    python tools/fill_chasu_from_db.py --db <SDSMDB.sqlite> --csv tools/chasu_fill_20261020.csv
        --write SDSM_Surveyor_App/sites.json --version sites-20261020          (한 줄로 친다)

⚠ `build_sites_json.py` 로 방류하천 항목을 다시 만들면 여기서 채운 값이 사라진다 — 그 뒤 이 도구를 다시 돌린다.
"""
from __future__ import annotations

import argparse
import collections
import csv
import datetime as _dt
import io
import json
import re
import sqlite3
import sys
import urllib.parse


def open_ro(path: str) -> sqlite3.Connection:
    return sqlite3.connect("file:" + urllib.parse.quote(path.replace("\\", "/")) + "?mode=ro", uri=True)


def year_of(year_chsu, survey_year):
    for v in (survey_year, year_chsu):
        m = re.match(r"\s*(\d{4})", str(v or ""))
        if m:
            return int(m.group(1))
    return None


def years(key: str) -> set[int]:
    """지점명 이력의 연도 키 — '22~25' · '26' · '07~10' · '2016~18'."""
    a, _, b = key.partition("~")

    def full(x: str, ref: int | None = None) -> int:
        x = x.strip()
        if len(x) == 4:
            return int(x)
        if ref is not None:
            return ref // 100 * 100 + int(x)
        return 2000 + int(x)

    y0 = full(a)
    y1 = full(b, y0) if b else y0
    return set(range(y0, y1 + 1))


def candidates(db_path: str, sites: list[dict]) -> list[dict]:
    con = open_ro(db_path)
    rows = con.execute(
        """select f.Site, f.YearChsu, f.SurveyYear, f.AutoRiverChasu, d.Project
           from Fish f left join SiteDivision d on d.SiteDivisionId = f.SiteDivisionId""").fetchall()
    con.close()
    recs = [(s, year_of(yc, sy), ch) for s, yc, sy, ch, p in rows if p == "방류하천" and ch is not None]
    bang = [s for s in sites if s.get("Project") == "방류하천"]

    def owners(name: str, y: int | None) -> set[str]:
        """그 해 그 이름을 옛 이름으로 쓰던 지점."""
        return {s["SiteName"] for s in bang for k, v in (s.get("YearAliases") or {}).items()
                if v == name and y is not None and y in years(k)}

    table = []
    for s in bang:
        if s.get("Chasu") not in (None, "", 0):
            continue
        name = s["SiteName"]
        aliases = [(k, v) for k, v in (s.get("YearAliases") or {}).items() if v != name]
        direct = [(y, ch) for n, y, ch in recs if n == name]
        via = [(y, ch, v) for k, v in aliases for n, y, ch in recs if n == v and y in years(k)]
        notes = []
        amb = sorted({o for y, _ in direct for o in owners(name, y) if o != name})
        if amb:
            notes.append("같은 이름을 그해 다른 지점(" + "·".join(amb) + ")이 옛 이름으로 씀")
        amb2 = sorted({o for y, _, v in via for o in owners(v, y) if o != name}
                      | {v for _, _, v in via if any(b["SiteName"] == v for b in bang)})
        if amb2:
            notes.append("옛 이름 기록이 다른 지점 이름과 겹침(" + "·".join(amb2) + ")")
        vals = collections.Counter([ch for _, ch in direct] + [ch for _, ch, _ in via])
        yrs = sorted({y for y, _ in direct if y} | {y for y, _, _ in via if y})
        if not vals:
            decision, value = "비움(기록 없음)", None
        elif len(vals) > 1:
            decision, value = "비움(값이 어긋남)", None
        elif notes:
            decision, value = "비움(지점명이 헷갈림)", None
        else:
            value = int(next(iter(vals)))
            decision = "채움"
        table.append({
            "지점": name, "하천": s.get("River"), "어류지점": "어류" in (s.get("Taxa") or []),
            "후보값": " · ".join(f"{int(k)}({n}건)" for k, n in sorted(vals.items())) or "-",
            "기록수": sum(vals.values()),
            "같은이름기록": len(direct), "옛이름기록": len(via),
            "옛이름": " · ".join(sorted({v for _, _, v in via})) or "-",
            "연도": f"{yrs[0]}~{yrs[-1]}" if yrs else "-",
            "판정": decision, "채울값": value, "비고": " / ".join(notes),
        })
    return table


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--db", required=True, help="게시 DB(읽기 전용으로 연다)")
    ap.add_argument("--sites", default="SDSM_Surveyor_App/sites.json", help="읽을 sites.json")
    ap.add_argument("--csv", help="판정표를 쓸 CSV(UTF-8 BOM)")
    ap.add_argument("--write", help="하천차수를 채워 쓸 sites.json(보통 --sites 와 같은 파일)")
    ap.add_argument("--version", help="--write 때 Version(예: sites-20261020)")
    args = ap.parse_args()
    sys.stdout.reconfigure(encoding="utf-8")

    with io.open(args.sites, encoding="utf-8-sig") as f:
        data = json.load(f)
    table = candidates(args.db, data["Sites"])
    for t in table:
        print(f"{t['하천']:<5} {t['지점']:<8} 후보 {t['후보값']:<12} 기록 {t['기록수']:>3} 연도 {t['연도']:<10} → {t['판정']}"
              + (f" {t['채울값']}" if t['채울값'] is not None else "") + (f"  ({t['비고']})" if t['비고'] else ""))

    if args.csv:
        with io.open(args.csv, "w", encoding="utf-8-sig", newline="") as f:
            w = csv.DictWriter(f, fieldnames=list(table[0].keys()), lineterminator="\n")
            w.writeheader()
            w.writerows(table)

    if args.write:
        fill = {t["지점"]: t["채울값"] for t in table if t["채울값"] is not None}
        n = 0
        for s in data["Sites"]:
            if s.get("Project") == "방류하천" and s.get("Chasu") in (None, "", 0) and s["SiteName"] in fill:
                s["Chasu"] = fill[s["SiteName"]]
                n += 1
        data["Version"] = args.version or f"sites-{_dt.date.today():%Y%m%d}"
        data["GeneratedAt"] = _dt.datetime.now().replace(microsecond=0).isoformat()
        with io.open(args.write, "w", encoding="utf-8", newline="\n") as f:
            json.dump(data, f, ensure_ascii=False, indent=2)
            f.write("\n")
        print(f"채움 {n}곳 · 비움 {len(table) - n}곳 · Version {data['Version']}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
