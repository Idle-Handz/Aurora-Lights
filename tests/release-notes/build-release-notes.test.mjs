import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";
import { mkdtempSync, mkdirSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";

import {
  compileReleaseNotes,
  normalizeFragment,
  renderReleaseNotes,
} from "../../tools/build-release-notes.mjs";

test("renderReleaseNotes groups entries in reader-facing category order", () => {
  const notes = renderReleaseNotes([
    {
      category: "fixed",
      note: "The second fix.",
      path: "docs/release-notes/fixed/z.md",
    },
    {
      category: "added",
      note: "A new feature.",
      path: "docs/release-notes/added/feature.md",
    },
    {
      category: "fixed",
      note: "The first fix.",
      path: "docs/release-notes/fixed/a.md",
    },
  ]);

  assert.equal(
    notes,
    [
      "## What’s new",
      "",
      "### Added",
      "",
      "- A new feature.",
      "",
      "### Fixed",
      "",
      "- The first fix.",
      "- The second fix.",
      "",
    ].join("\n"),
  );
});

test("normalizeFragment accepts a wrapped sentence and removes an accidental bullet", () => {
  assert.equal(
    normalizeFragment("- A useful change\n  with more detail.", "fragment.md"),
    "A useful change with more detail.",
  );
  assert.throws(
    () => normalizeFragment("\n", "empty.md"),
    /Release-note fragment is empty: empty\.md/,
  );
});

test("compileReleaseNotes includes only fragments added after the previous tag", () => {
  const repositoryRoot = mkdtempSync(join(tmpdir(), "aurora-release-notes-"));

  try {
    execFileSync("git", ["init", "--quiet"], { cwd: repositoryRoot });
    execFileSync("git", ["config", "user.email", "tests@aurora.invalid"], {
      cwd: repositoryRoot,
    });
    execFileSync("git", ["config", "user.name", "Aurora Tests"], {
      cwd: repositoryRoot,
    });
    execFileSync("git", ["config", "core.autocrlf", "false"], {
      cwd: repositoryRoot,
    });

    const fixedDirectory = join(
      repositoryRoot,
      "docs",
      "release-notes",
      "fixed",
    );
    mkdirSync(fixedDirectory, { recursive: true });
    const publishedFragment = join(fixedDirectory, "published.md");
    writeFileSync(publishedFragment, "An already published fix.\n", "utf8");
    execFileSync("git", ["add", "."], { cwd: repositoryRoot });
    execFileSync("git", ["commit", "--quiet", "-m", "Published release"], {
      cwd: repositoryRoot,
    });
    execFileSync("git", ["tag", "v1.0.0"], { cwd: repositoryRoot });

    writeFileSync(
      publishedFragment,
      "Edited wording for an already published fix.\n",
      "utf8",
    );
    writeFileSync(
      join(fixedDirectory, "new.md"),
      "A newly added fix.\n",
      "utf8",
    );
    execFileSync("git", ["add", "."], { cwd: repositoryRoot });
    execFileSync("git", ["commit", "--quiet", "-m", "Next release"], {
      cwd: repositoryRoot,
    });

    assert.equal(
      compileReleaseNotes({
        repositoryRoot,
        from: "v1.0.0",
      }),
      ["## What’s new", "", "### Fixed", "", "- A newly added fix.", ""].join(
        "\n",
      ),
    );
  } finally {
    rmSync(repositoryRoot, { recursive: true, force: true });
  }
});
