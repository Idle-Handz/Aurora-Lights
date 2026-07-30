# Release-note fragments

Add one Markdown file for each user-visible change. Put it in the directory that
matches the heading it should receive:

- `added`
- `improved`
- `fixed`
- `release-process`

Each fragment contains one plain-language release-note sentence. Do not include
a heading; a leading Markdown bullet is optional. Use a new, descriptively named
file instead of editing a fragment that has already appeared in a release.

When a release is created, `tools/build-release-notes.mjs` selects fragments
added since the previous published release tag and prepends the compiled list to
GitHub's automatically generated release notes. Because selection is based on
Git history, published fragments do not need to be deleted or archived.
