# Hugo Regression Safety Net

This document defines the first implementation slice of regression protection for the Hugo website under `src/`.

## Current coverage

The `Hugo Regression Preflight` workflow adds report-driven checks for pull requests and pushes that touch Hugo content, layouts, configuration, theme assets, or regression scripts.

Checks included in this phase:

- Hugo production build with warnings captured to an artefact
- warning delta report against `.hugo-warnings-baseline`
- front matter asset reference validation (non-blocking while baseline is established)

## Workflow

- Workflow file: `.github/workflows/hugo-regression-preflight.yml`
- Artefact name: `hugo-regression-preflight-reports`
- Report files:
  - `build/hugo-build.log`
  - `build/hugo-warnings.log`
  - `build/warning-delta.txt`
  - `build/missing-assets.txt`

## Local execution

Scripts are Bash scripts and run natively on Linux/macOS, WSL, or Git Bash.

Run from repository root:

```bash
CHECK_MODE=report scripts/check-build-warnings.sh .hugo-warnings-baseline build/hugo-warnings.log build/warning-delta.txt
scripts/validate-hugo-assets.sh src/content src/static build/missing-assets.txt
```

If you run on Windows PowerShell without WSL/Git Bash, use the CI artefacts as the source of truth until a PowerShell wrapper is introduced.

## Baseline governance

- Keep `.hugo-warnings-baseline` sorted and minimal.
- Add entries only for intentional, accepted warnings.
- Baseline changes must be made in pull requests that explicitly describe why the warning is acceptable.

## Next implementation slices

- Promote asset validation to merge-blocking once the current baseline is clean.
- Add internal link validation on generated output.
- Add deterministic HTML snapshot regression checks.
- Add targeted Playwright screenshot regression checks for key templates and viewports.
