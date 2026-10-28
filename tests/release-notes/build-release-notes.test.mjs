import assert from "node:assert/strict";
import { execFileSync, spawnSync } from "node:child_process";
import {
  existsSync,
  mkdtempSync,
  mkdirSync,
  readFileSync,
  rmSync,
  writeFileSync,
} from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";

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

test("compileReleaseNotes requires new fragments when requested and reads the selected revision", () => {
  const repositoryRoot = mkdtempSync(join(tmpdir(), "aurora-release-notes-"));
  const outputPath = join(repositoryRoot, "release-notes.md");
  const runCompiler = (...args) =>
    spawnSync(
      process.execPath,
      [
        fileURLToPath(new URL("../../tools/build-release-notes.mjs", import.meta.url)),
        ...args,
      ],
      { cwd: repositoryRoot, encoding: "utf8" },
    );

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

    assert.equal(compileReleaseNotes({ repositoryRoot, from: "v1.0.0" }), "");
    assert.throws(
      () => compileReleaseNotes({
        repositoryRoot,
        from: "v1.0.0",
        requireFragments: true,
      }),
      /No release-note fragments were added since v1\.0\.0/,
    );

    writeFileSync(
      publishedFragment,
      "Edited wording for an already published fix.\n",
      "utf8",
    );
    const archiveDirectory = join(
      repositoryRoot,
      "docs/release-notes/published/v1.0.0/fixed",
    );
    mkdirSync(archiveDirectory, { recursive: true });
    writeFileSync(join(archiveDirectory, "backfilled.md"), "A historical fix.\n", "utf8");
    writeFileSync(
      join(repositoryRoot, "docs/release-notes/README.md"),
      "Fragment instructions.\n",
      "utf8",
    );
    execFileSync("git", ["add", "."], { cwd: repositoryRoot });
    execFileSync("git", ["commit", "--quiet", "-m", "Restore historical notes"], {
      cwd: repositoryRoot,
    });

    const missingNotes = runCompiler(
      "--require-fragments", "--from", "v1.0.0", "--output", outputPath,
    );
    assert.equal(missingNotes.status, 1);
    assert.match(
      missingNotes.stderr,
      /No release-note fragments were added since v1\.0\.0/,
    );
    assert.match(missingNotes.stderr, /Add a new, non-empty Markdown file/);
    assert.equal(existsSync(outputPath), false);

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
        requireFragments: true,
      }),
      ["## What’s new", "", "### Fixed", "", "- A newly added fix.", ""].join(
        "\n",
      ),
    );

    const withNotes = runCompiler(
      "--from", "v1.0.0", "--require-fragments", "--output", outputPath,
    );
    assert.equal(withNotes.status, 0, withNotes.stderr);
    assert.equal(
      readFileSync(outputPath, "utf8"),
      compileReleaseNotes({ repositoryRoot, from: "v1.0.0", requireFragments: true }),
    );

    // The requested release tree, not today's checkout/index, owns its fragments.
    assert.equal(
      compileReleaseNotes({
        repositoryRoot,
        from: "(none)",
        to: "v1.0.0",
        requireFragments: true,
      }),
      ["## What’s new", "", "### Fixed", "", "- An already published fix.", ""].join("\n"),
    );
    writeFileSync(join(fixedDirectory, "new.md"), "Uncommitted future wording.\n", "utf8");
    assert.equal(
      compileReleaseNotes({ repositoryRoot, from: "v1.0.0", to: "HEAD" }),
      ["## What’s new", "", "### Fixed", "", "- A newly added fix.", ""].join("\n"),
    );
    writeFileSync(join(fixedDirectory, "empty.md"), "\n  \n", "utf8");
    execFileSync("git", ["add", "docs/release-notes/fixed/empty.md"], { cwd: repositoryRoot });
    execFileSync("git", ["commit", "--quiet", "-m", "Add an invalid empty fragment"], {
      cwd: repositoryRoot,
    });
    const emptyNote = runCompiler("--from", "v1.0.0", "--require-fragments");
    assert.equal(emptyNote.status, 1);
    assert.match(emptyNote.stderr, /Release-note fragment is empty/);
  } finally {
    rmSync(repositoryRoot, { recursive: true, force: true });
  }
});
