import { execFileSync } from "node:child_process";
import { readFileSync, writeFileSync } from "node:fs";
import { isAbsolute, resolve } from "node:path";
import { pathToFileURL } from "node:url";

export const RELEASE_NOTE_CATEGORIES = Object.freeze([
  { directory: "added", title: "Added" },
  { directory: "improved", title: "Improved" },
  { directory: "fixed", title: "Fixed" },
  { directory: "release-process", title: "Release process" },
]);

const releaseNotePrefix = "docs/release-notes/";

export function categoryForPath(filePath) {
  const normalizedPath = filePath.replaceAll("\\", "/");
  return RELEASE_NOTE_CATEGORIES.find(
    ({ directory }) =>
      normalizedPath.startsWith(`${releaseNotePrefix}${directory}/`) &&
      normalizedPath.endsWith(".md"),
  );
}

export function normalizeFragment(content, filePath) {
  const note = content
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter(Boolean)
    .join(" ");

  if (!note) {
    throw new Error(`Release-note fragment is empty: ${filePath}`);
  }

  return note.replace(/^[-*]\s+/, "");
}

export function renderReleaseNotes(fragments) {
  const sections = RELEASE_NOTE_CATEGORIES.map((category) => {
    const entries = fragments
      .filter((fragment) => fragment.category === category.directory)
      .sort((left, right) => left.path.localeCompare(right.path));

    if (entries.length === 0) {
      return "";
    }

    return [
      `### ${category.title}`,
      "",
      ...entries.map((entry) => `- ${entry.note}`),
    ].join("\n");
  }).filter(Boolean);

  if (sections.length === 0) {
    return "";
  }

  return ["## What’s new", ...sections].join("\n\n") + "\n";
}

export function findReleaseNotePaths({
  repositoryRoot,
  from,
  to = "HEAD",
  runGit = execFileSync,
}) {
  const args =
    from && from !== "(none)"
      ? [
          "diff",
          "--diff-filter=A",
          "--name-only",
          "-z",
          `${from}..${to}`,
          "--",
          `${releaseNotePrefix}*/**.md`,
        ]
      : ["ls-files", "-z", `${releaseNotePrefix}*/**.md`];

  const output = runGit("git", args, {
    cwd: repositoryRoot,
    encoding: "utf8",
  });

  return output
    .split("\0")
    .filter(Boolean)
    .filter((filePath) => categoryForPath(filePath))
    .sort();
}

export function compileReleaseNotes({
  repositoryRoot,
  from,
  to = "HEAD",
  runGit = execFileSync,
}) {
  const fragments = findReleaseNotePaths({
    repositoryRoot,
    from,
    to,
    runGit,
  }).map((filePath) => {
    const category = categoryForPath(filePath);
    const absolutePath = resolve(repositoryRoot, filePath);

    return {
      category: category.directory,
      note: normalizeFragment(readFileSync(absolutePath, "utf8"), filePath),
      path: filePath,
    };
  });

  return renderReleaseNotes(fragments);
}

function parseArguments(args) {
  const options = {
    repositoryRoot: process.cwd(),
    from: "",
    to: "HEAD",
    output: "",
  };

  for (let index = 0; index < args.length; index += 1) {
    const argument = args[index];
    const value = args[index + 1];

    if (!["--root", "--from", "--to", "--output"].includes(argument) || !value) {
      throw new Error(`Unsupported or incomplete argument: ${argument}`);
    }

    index += 1;
    if (argument === "--root") options.repositoryRoot = resolve(value);
    if (argument === "--from") options.from = value;
    if (argument === "--to") options.to = value;
    if (argument === "--output") options.output = value;
  }

  return options;
}

function main() {
  const options = parseArguments(process.argv.slice(2));
  const notes = compileReleaseNotes(options);

  if (options.output) {
    const outputPath = isAbsolute(options.output)
      ? options.output
      : resolve(options.repositoryRoot, options.output);
    writeFileSync(outputPath, notes, "utf8");
  } else {
    process.stdout.write(notes);
  }
}

const invokedPath = process.argv[1]
  ? pathToFileURL(resolve(process.argv[1])).href
  : "";
if (import.meta.url === invokedPath) {
  main();
}
