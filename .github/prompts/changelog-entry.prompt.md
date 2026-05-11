---
description: "Use when drafting Keep a Changelog compliant Unreleased entries from commits, merged PRs, or release prep notes"
name: "Changelog Entry"
argument-hint: "List of merged changes or commit summaries to categorise"
agent: "agent"
---
Draft updates for `CHANGELOG.md` under the `Unreleased` section using Keep a Changelog categories.

Task constraints:
- Categorise items under Added, Changed, Deprecated, Removed, Fixed, or Security.
- Keep entries concise, user-relevant, and specific.
- Avoid duplicate or speculative statements.
- Preserve the existing heading structure.
- Write all new text in British English.

Execution guidance:
1. Map each input change to a single best-fit category.
2. Draft bullet points in consistent style.
3. Flag ambiguous items needing clarification.
4. Output the proposed markdown block ready to apply.
