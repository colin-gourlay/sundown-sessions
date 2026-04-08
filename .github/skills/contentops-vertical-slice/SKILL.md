---
name: contentops-vertical-slice
description: "Use when implementing a focused feature or bug fix in automation/dotnet as a clean architecture vertical slice across Domain, Application, Infrastructure, and tests."
argument-hint: "Problem statement, acceptance criteria, and constraints"
user-invocable: true
disable-model-invocation: false
---

# ContentOps Vertical Slice

## When to Use

- Implementing a single feature or defect fix in `automation/dotnet/`.
- Adding behaviour that spans one or more clean architecture layers.
- Updating tests to capture behavioural changes.

## Do Not Use When

- The task is purely Hugo editorial/content authoring under `src/content/`.
- The request is limited to markdown changelog or release-note drafting.
- The work is a broad architecture redesign rather than a focused vertical slice.

## Procedure

1. Trace current flow from CLI entrypoint through application handlers to domain and infrastructure dependencies.
2. Identify the smallest set of files needed to implement the requested behaviour.
3. Implement layer-appropriate changes while preserving existing boundaries.
4. Add or update tests in the corresponding test project.
5. Run focused verification commands and summarise outcomes.

## Quality Checklist

- Responsibilities remain correctly separated by layer.
- Expected failures use explicit result patterns.
- Tests cover the changed behaviour.
- New human-facing text and comments use British English.
