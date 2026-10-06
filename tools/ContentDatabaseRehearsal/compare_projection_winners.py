"""Compare which declaration wins each element id in the legacy loader and in the database.

Legacy replaces an element outright when a later file re-declares its id, so the legacy catalogue
is the oracle: whatever it resolves an id to is what the database projection has to resolve it to.

Usage:
  python compare_projection_winners.py <legacy-dump-dir> <database-dump-dir>

Each directory is the REHEARSAL_OUTPUT of a harness run - "legacy-dump" for the first, "dump" for
the second - and holds a projection-dump.json. Exit code 0 means IDs and XML fingerprints match;
1 means a missing ID or XML definition difference needs review. These fingerprints do not prove
runtime equivalence: Legacy can append parsed properties without updating its source ElementNode.

Disagreements are reported in three buckets:
  missing      an id one path has and the other does not - content lost on one side
  label-only   same definition, different provenance label - harmless (unstamped builtins, and
               identical duplicate declarations consolidated under a different file's name)
  definition   the serialized definition differs, even when the same file won
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

    groups = collections.defaultdict(lambda: {"label_only": 0, "definition": []})
    for element_id in set(legacy) & set(database):
        if (legacy[element_id]["prov"] == database[element_id]["prov"]
                and legacy[element_id]["fp"] == database[element_id]["fp"]):
            continue
        group = groups[(label(legacy[element_id]), label(database[element_id]))]
        if legacy[element_id]["fp"] == database[element_id]["fp"]:
            group["label_only"] += 1
        else:
            group["definition"].append(element_id)

    definition_differences = sorted(i for g in groups.values() for i in g["definition"])
    print("legacy ids %d, database ids %d" % (len(legacy), len(database)))
    print("missing from database : %d" % len(only_legacy))
    print("missing from legacy   : %d" % len(only_database))
    print("label-only            : %d" % sum(g["label_only"] for g in groups.values()))
    print("definition differences: %d" % len(definition_differences))

    for (won_legacy, won_database), group in sorted(
            groups.items(), key=lambda kv: -(kv[1]["label_only"] + len(kv[1]["definition"]))):
        print()
        print("  legacy   %s" % won_legacy)
        print("  database %s" % won_database)
        print("           label-only %d, definition differences %d" % (group["label_only"], len(group["definition"])))
        for element_id in sorted(group["definition"]):
            print("             %s" % element_id)

    for element_id in only_legacy:
        print("MISSING FROM DATABASE: %s" % element_id)
    for element_id in only_database:
        print("MISSING FROM LEGACY:   %s" % element_id)

    return 1 if definition_differences or only_legacy or only_database else 0


if __name__ == "__main__":
    if len(sys.argv) != 3:
        sys.exit(__doc__)
    sys.exit(main(sys.argv[1], sys.argv[2]))
