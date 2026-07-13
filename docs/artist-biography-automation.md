# Artist Biography Automation

The artist biography audit workflow is deliberately conservative: it does not rewrite
artist biographies by itself. It scans the catalogue, regenerates the review report,
and opens a pull request when the scheduled report changes.

## Why the workflow does not auto-write biographies

Updating artist biographies safely requires editorial judgement and a trusted source
for new facts. GitHub Actions can edit files and raise pull requests, but it cannot
know whether an artist biography is accurate unless the repository provides one of
these inputs:

- curated source data checked into the repository;
- a deterministic enrichment script that maps known artist metadata to copy;
- an approved external API or AI authoring service configured with credentials,
  source-attribution rules and human review safeguards.

Without one of those inputs, automatically rewriting `src/content/artists/**/index.md`
would risk replacing accurate copy with unsupported or hallucinated content.

## What the scheduled workflow does now

On the weekly schedule and on manual runs, `.github/workflows/audit-artist-biographies.yml`:

1. checks out the repository;
2. runs `scripts/audit-artist-biographies.py`;
3. uploads the generated report as a workflow artifact;
4. opens a pull request containing the regenerated `reports/artist-biography-audit.md`
   when that report changes.

The pull request is the review queue. It shows which artist pages need a human pass,
and those pages can then be refreshed in a focused content PR.

## What would be needed for automatic biography-update PRs

A future workflow could update artist biographies directly and raise a PR, but it
should only do so after adding a trusted authoring source. A safe design would be:

1. generate a candidate list from the audit report;
2. enrich each candidate from approved sources or curated metadata;
3. write proposed biography changes and update `lastReviewed` only for pages that
   were actually changed;
4. create a pull request for editorial review;
5. require the normal Hugo/content validation checks before merge.

Until that source-of-truth layer exists, the repository should keep the scheduled
workflow in audit-and-PR mode rather than auto-authoring biographies.
