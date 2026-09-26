"""Compare which declaration wins each element id in the legacy loader and in the database.

Legacy replaces an element outright when a later file re-declares its id, so the legacy catalogue
is the oracle: whatever it resolves an id to is what the database projection has to resolve it to.

Usage:
  python compare_projection_winners.py <legacy-dump-dir> <database-dump-dir>

Each directory is the REHEARSAL_OUTPUT of a harness run - "legacy-dump" for the first, "dump" for
the second - and holds a projection-dump.json. Exit code 0 means the two paths agree on every id
that matters; 1 means at least one id resolves to a different definition.

Disagreements are reported in three buckets:
  missing      an id one path has and the other does not - content lost on one side
  label-only   same definition, different provenance label - harmless (unstamped builtins, and
               identical duplicate declarations consolidated under a different file's name)
  behavioural  a different file won and the definition differs - the ones to fix
"""
import collections
import json
import pathlib
import sys


def load(directory):
    path = pathlib.Path(directory) / "projection-dump.json"
    byid = collections.defaultdict(lambda: {"fp": set(), "prov": set()})
    for row in json.loads(path.read_text(encoding="utf-8-sig")):
        byid[row["Id"]]["fp"].add(row.get("fingerprint") or "")
        byid[row["Id"]]["prov"].add(row.get("provenance") or "")
    return byid


def label(entry):
    return " | ".join(sorted(entry["prov"])) or "(unstamped)"


def main(legacy_dir, database_dir):
    legacy, database = load(legacy_dir), load(database_dir)

    only_legacy = sorted(set(legacy) - set(database))
    only_database = sorted(set(database) - set(legacy))

    groups = collections.defaultdict(lambda: {"label_only": 0, "behavioural": []})
    for element_id in set(legacy) & set(database):
        if legacy[element_id]["prov"] == database[element_id]["prov"]:
            continue
        group = groups[(label(legacy[element_id]), label(database[element_id]))]
        if legacy[element_id]["fp"] == database[element_id]["fp"]:
            group["label_only"] += 1
        else:
            group["behavioural"].append(element_id)

    behavioural = sorted(i for g in groups.values() for i in g["behavioural"])
    print("legacy ids %d, database ids %d" % (len(legacy), len(database)))
    print("missing from database : %d" % len(only_legacy))
    print("missing from legacy   : %d" % len(only_database))
    print("label-only            : %d" % sum(g["label_only"] for g in groups.values()))
    print("behavioural           : %d" % len(behavioural))

    for (won_legacy, won_database), group in sorted(
            groups.items(), key=lambda kv: -(kv[1]["label_only"] + len(kv[1]["behavioural"]))):
        print()
        print("  legacy   %s" % won_legacy)
        print("  database %s" % won_database)
        print("           label-only %d, behavioural %d" % (group["label_only"], len(group["behavioural"])))
        for element_id in sorted(group["behavioural"]):
            print("             %s" % element_id)

    for element_id in only_legacy:
        print("MISSING FROM DATABASE: %s" % element_id)
    for element_id in only_database:
        print("MISSING FROM LEGACY:   %s" % element_id)

    return 1 if behavioural or only_legacy or only_database else 0


if __name__ == "__main__":
    if len(sys.argv) != 3:
        sys.exit(__doc__)
    sys.exit(main(sys.argv[1], sys.argv[2]))
