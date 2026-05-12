---
description: "Use when implementing a focused ContentOps feature, bug fix, or command-handler change in automation/dotnet as a clean architecture vertical slice with tests"
name: "ContentOps Feature Slice"
argument-hint: "Feature or bug scope, acceptance criteria, and constraints"
agent: "agent"
---
Implement one focused change in `automation/dotnet/` using the repository's clean architecture boundaries.

Task constraints:
- Keep domain rules in Domain, orchestration in Application, and technical integrations in Infrastructure.
- Maintain existing command/handler and result patterns.
- Add or update tests that prove the behavioural change.
- Avoid unrelated refactors.
- Write all new human-facing text in British English, including comments and output copy.

Execution guidance:
1. Identify the minimal set of files needed for the change.
2. Implement the behaviour and tests together.
3. Run or propose targeted verification commands.
4. Summarise changed files, tests, and residual risks.
