# Contributing

Thank you for contributing to Sundown Sessions.

This guide defines the repository branching convention and pull request workflow for both workstreams:

- `src/` (Hugo website)
- `automation/dotnet/` (ContentOps automation)

## Branching Model

This repository uses trunk-based development.

- `main` is the only long-lived branch.
- All changes are made on short-lived branches.
- Merge changes to `main` through pull requests.
- Keep branches small and focused to reduce review and merge risk.

## Branch Naming Convention

Use the format:

`type/workstream/short-description`

Examples:

- `feat/src/add-artist-social-links`
- `fix/src/correct-show-ordering`
- `docs/repo/clarify-local-setup`
- `feat/automation-dotnet/add-release-validation`
- `refactor/automation-dotnet/simplify-command-handler`

### Allowed Type Values

The branch `type` should align with the conventional commit categories used in pull requests:

- `feat`
- `fix`
- `docs`
- `chore`
- `refactor`
- `test`
- `build`
- `ci`

### Workstream Values

Use a clear workstream segment that reflects the area being changed:

- `src` for Hugo website work
- `automation-dotnet` for ContentOps work
- `repo` for repository-level changes that span both workstreams

### Short Description Rules

Use a concise, hyphenated summary:

- lower-case letters, numbers, and hyphens only
- describe intent, not implementation detail
- keep it brief and readable

## Pull Request Convention

Create pull requests from your short-lived branch into `main`.

### Title Format

Pull request titles must follow conventional commits:

`type(scope): summary`

or:

`type: summary`

Accepted `type` values are:

- `feat`
- `fix`
- `docs`
- `chore`
- `refactor`
- `test`
- `build`
- `ci`

Examples:

- `feat(contentops): add release validation command`
- `fix(website): correct show ordering logic`
- `docs: define repository branching convention`

### Description Quality

Pull request descriptions should clearly explain intent, context, and impact.

### Changelog Expectation for ContentOps

If your pull request changes files under `automation/dotnet/`, update [CHANGELOG.md](CHANGELOG.md) when behaviour or release notes are affected.

## Release and Deployment Notes

### Website

- Production deployment is triggered from `main`.

### ContentOps

- Releases can be created manually via workflow dispatch.
- Tag-based releases use the `contentops/v*` naming pattern.

## Additional Repository Standards

- Use British English in documentation and user-facing text.
- Keep website and automation concerns separated unless a change explicitly requires both.
- Keep edits scoped and avoid unrelated refactoring.

For architecture and setup details, see [README.md](README.md) and [automation/dotnet/README.md](automation/dotnet/README.md).

## Discussions

Design proposals, ideas, Q&A, and RFC feedback are handled through GitHub Discussions. This is the right place for exploratory thinking before work is scoped into issues.

See [docs/discussions.md](docs/discussions.md) for:

- Category descriptions and when to use each one
- How to use the structured discussion templates
- Moderation guidance (marking answers, locking threads, converting to issues)
