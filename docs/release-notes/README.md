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

The release workflow requires at least one new, non-empty fragment since the
previous published release before it starts building packages or creates a draft.
Editing an existing fragment or adding notes to the published archive does not
satisfy this check. To run the same check locally against committed changes:

```sh
node tools/build-release-notes.mjs --from <previous-release-tag> --to HEAD --require-fragments
```

If fragments are restored after a release has already been published, put them
under `published/<release-tag>/<category>/` using the same sentence format and
categories. Render those fragments with `renderReleaseNotes` from
`tools/build-release-notes.mjs` and prepend them to that release's existing GitHub
description. This archive sits outside the active category directories so the
restored notes are not collected again for the next release.
