---
description: "Use when generating ContentOps release notes, asset summaries, and operator guidance aligned with workflow outputs"
name: "ContentOps Release Notes"
argument-hint: "Version, major changes, assets, and operational notes"
agent: "agent"
---
Prepare release notes for a ContentOps release aligned with `.github/workflows/contentops-release.yml` outputs.

Task constraints:
- Include version scope and notable behavioural changes.
- Summarise release assets and runtime usage guidance.
- Keep wording practical for operators and contributors.
- Note compatibility or deployment caveats where relevant.
- Write all new text in British English.

Execution guidance:
1. Structure notes as summary, changes, assets, and usage.
2. Keep claims evidence-based and avoid overstatement.
3. Include brief post-release verification guidance.
4. Output markdown suitable for a GitHub release body.
