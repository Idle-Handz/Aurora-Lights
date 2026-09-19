"""Compare two content databases table by table as multisets of rows.

Usage:
  python compare_databases.py A.sqlite B.sqlite [--root-a DIR] [--root-b DIR]
                              [--ignore TABLE.COLUMN ...] [--out report.json]

--root-a/--root-b replace each database's content-root prefix in text values with
<root>, so copies built in different directories compare by relative location.
--ignore drops a column from comparison; use *.COLUMN to drop it from every table.
Exit code 0 means every table matched and both databases pass integrity checks.
"""
import argparse
import collections
import json
import sqlite3
import sys


def open_readonly(path):
    return sqlite3.connect("file:" + path.replace("\\", "/") + "?mode=ro", uri=True)


def tables(connection):
    return [r[0] for r in connection.execute(
        "select name from sqlite_master where type='table' and name not like 'sqlite_%' order by name")]


def columns(connection, table):
    return [r[1] for r in connection.execute(f'pragma table_info("{table}")')]


def normalize(value, roots):
    if isinstance(value, str):
        for root in roots:
            if root and root in value:
                value = value.replace(root, "<root>")
    return value


def rows(connection, table, keep, roots):
    names = columns(connection, table)
    indexes = [names.index(c) for c in keep]
    return collections.Counter(
        tuple(normalize(row[i], roots) for i in indexes)
        for row in connection.execute(f'select * from "{table}"'))


def root_variants(root):
    return [root, root.replace("\\", "/")] if root else []


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("a")
    parser.add_argument("b")
    parser.add_argument("--root-a", default="")
    parser.add_argument("--root-b", default="")
    parser.add_argument("--ignore", action="append", default=[])
    parser.add_argument("--out")
    options = parser.parse_args()

    a, b = open_readonly(options.a), open_readonly(options.b)
    ignored = set(options.ignore)
    report = {"a": options.a, "b": options.b, "ignored": sorted(ignored), "tables": [], "differences": []}
    for label, connection in [("a", a), ("b", b)]:
        report[label + "Integrity"] = connection.execute("pragma integrity_check").fetchone()[0]
        report[label + "ForeignKeyErrors"] = len(connection.execute("pragma foreign_key_check").fetchall())

    table_names = sorted(set(tables(a)) | set(tables(b)))
    for table in table_names:
        in_a, in_b = table in tables(a), table in tables(b)
        if not (in_a and in_b):
            report["differences"].append({"table": table, "missingFrom": "b" if in_a else "a"})
            continue
        cols_a, cols_b = columns(a, table), columns(b, table)
        if cols_a != cols_b:
            report["differences"].append({"table": table, "columnsA": cols_a, "columnsB": cols_b})
            continue
        keep = [c for c in cols_a if f"{table}.{c}" not in ignored and f"*.{c}" not in ignored]
        x = rows(a, table, keep, root_variants(options.root_a))
        y = rows(b, table, keep, root_variants(options.root_b))
        entry = {"table": table, "rowsA": sum(x.values()), "rowsB": sum(y.values())}
        report["tables"].append(entry)
        if x != y:
            only_a, only_b = x - y, y - x
            report["differences"].append({
                "table": table, "columns": keep,
                "onlyInA": sum(only_a.values()), "onlyInB": sum(only_b.values()),
                "sampleA": [list(r) for r in list(only_a)[:5]],
                "sampleB": [list(r) for r in list(only_b)[:5]],
            })

    report["tablesCompared"] = len(report["tables"])
    report["identical"] = (not report["differences"] and report["aIntegrity"] == "ok"
                           and report["bIntegrity"] == "ok"
                           and report["aForeignKeyErrors"] == 0 and report["bForeignKeyErrors"] == 0)
    text = json.dumps(report, indent=2, default=str)
    if options.out:
        with open(options.out, "w", encoding="utf-8") as handle:
            handle.write(text)
    summary = {k: report[k] for k in ["tablesCompared", "identical", "aIntegrity", "bIntegrity",
                                      "aForeignKeyErrors", "bForeignKeyErrors"]}
    summary["differingTables"] = [d["table"] for d in report["differences"]]
    print(json.dumps(summary, indent=2))
    sys.exit(0 if report["identical"] else 1)


if __name__ == "__main__":
    main()
