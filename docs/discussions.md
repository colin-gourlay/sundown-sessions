# GitHub Discussions — Setup and Moderation Guide

This document describes how GitHub Discussions is used in the `colin-gourlay/sundown-sessions` repository, including one-time setup steps that require manual action in the GitHub UI, and ongoing moderation guidance for maintainers.

## Why Discussions?

Discussions provide a structured, open space for design thinking, community input, and early ideation — separate from the issue tracker. The goal is to:

- Record architectural and product decisions in the open (ADR-style)
- Encourage brainstorming before work is scoped into issues
- Build a searchable knowledge base of Q&A
- Showcase work in progress

## One-Time Setup (Manual Steps for Repository Owner)

The following steps cannot be automated via repository files and require admin access to the repository settings.

### 1. Enable Discussions

1. Go to **Settings → General → Features**
2. Check the **Discussions** checkbox
3. Click **Save changes**

Reference: [About GitHub Discussions](https://docs.github.com/en/discussions/collaborating-with-your-community-using-discussions/about-discussions)

### 2. Create Discussion Categories

Navigate to the **Discussions** tab, then click the pencil icon next to **Categories** to manage them.

Create the following categories (delete any default categories that do not fit):

| Category | Format | Purpose |
|---|---|---|
| 📣 Announcements | Announcement | One-to-many updates from maintainers. Pin key threads here. |
| 💡 Ideas / Brainstorming | Open-ended discussion | Early-stage ideas and discovery. No solution required. |
| 📐 Design Proposals | Open-ended discussion | ADR-style structured decision records. Use the Design Proposal template. |
| ❓ Q&A | Question / Answer | Questions about the project. Mark answers to build a knowledge base. |
| 🎬 Show and Tell | Open-ended discussion | Share work in progress, screenshots, demos, and experiments. |
| 🗳️ RFC Feedback / Polls | Poll | Structured feedback requests. Use polls to gauge preference when helpful. |

> **Note:** The **Announcements** format restricts replies to maintainers and collaborators only, which is appropriate for that category.

### 3. Pin Seed Discussions

After creating categories, create and pin the following two discussions to anchor the space.

#### "Start here — How we make decisions"

- **Category:** 📣 Announcements
- **Title:** `Start here — How we make decisions`
- **Body:** (suggested content below — adapt as needed)

```markdown
Welcome to Sundown Sessions Discussions. This is the space where design thinking, product ideas, and architectural decisions happen in the open.

## How we use Discussions

| Category | Use it for |
|---|---|
| 💡 Ideas / Brainstorming | Early-stage thoughts, half-formed concepts, things worth exploring |
| 📐 Design Proposals | Structured decisions (ADR-style) — problem, options, tradeoffs, decision |
| ❓ Q&A | Questions about how the project works or how to contribute |
| 🎬 Show and Tell | Demos, screenshots, work in progress |
| 🗳️ RFC Feedback / Polls | Concrete proposals that need community input before committing |

## How decisions are made

1. Start an **Idea** discussion to explore early thinking
2. When the idea has enough shape, open a **Design Proposal** using the template
3. Gather feedback; update the Decision section when consensus is reached
4. Convert to an issue (via the "Create issue from discussion" button) when there is a clear path to execution
5. Maintainers close or lock the discussion thread once the issue is created or the decision is recorded

## Moderation norms

- **Q&A**: Maintainers and contributors mark the best answer. Concluded threads are locked.
- **Announcements**: Maintainer-only replies. Pin key announcements.
- **Design Proposals**: The original author updates the Decision field when a conclusion is reached. The thread is then locked.
```

Pin this discussion from the **⋯** menu on the discussion card.

#### "Roadmap and priorities"

- **Category:** 📣 Announcements
- **Title:** `Roadmap and priorities`
- **Body:** (suggested content below — adapt as needed)

```markdown
This discussion tracks the current direction and priorities for Sundown Sessions.

The canonical roadmap is maintained in [README.md](../README.md#roadmap). This thread is the place to comment on priorities, raise concerns, or suggest additions before they are reflected there.

## Now

- Improve contributor onboarding across both workstreams
- Keep website content structure consistent for artists and shows
- Stabilise ContentOps release and runtime guidance

## Next

- Expand ContentOps beyond initial album intake workflows
- Strengthen operational tracing and observability in automation flows
- Improve editorial tooling and content curation ergonomics

## Later

- Increase reuse of Spotify and Lidarr integration libraries across additional workflows
- Extend website discovery and presentation capabilities for long-tail catalogue content
- Formalise richer contributor documentation as the platform footprint grows

---

_Last updated: April 2025. Comment below to raise a priority or flag something missing._
```

Pin this discussion from the **⋯** menu on the discussion card.

---

## Discussion Templates

Structured templates are available for the following categories. GitHub loads them automatically when a contributor opens a new discussion in a matching category.

| Template file | Category to use it with |
|---|---|
| `.github/DISCUSSION_TEMPLATE/design-proposal.yml` | 📐 Design Proposals |
| `.github/DISCUSSION_TEMPLATE/ideas.yml` | 💡 Ideas / Brainstorming |
| `.github/DISCUSSION_TEMPLATE/rfc-feedback.yml` | 🗳️ RFC Feedback / Polls |
| `.github/DISCUSSION_TEMPLATE/show-and-tell.yml` | 🎬 Show and Tell |
| `.github/DISCUSSION_TEMPLATE/q-and-a.yml` | ❓ Q&A |

> **Note:** GitHub Discussions templates in `.github/DISCUSSION_TEMPLATE/` are loaded by category name matching. Ensure the category slugs in GitHub match the template file names for automatic loading to work. If auto-loading is not available in your GitHub plan tier, contributors can still use the templates manually as a guide.

---

## Ongoing Moderation Guidance

### Marking Answers (Q&A)

In the **Q&A** category, once a satisfactory answer has been posted, maintainers or the original author should click **Mark as answer** on the best reply. This:

- Surfaces the answer prominently in the thread
- Helps future visitors find the answer quickly
- Enables filtering by "Answered" or "Unanswered" discussions

Lock the thread after marking an answer if no further discussion is needed.

### Locking Concluded Threads

Lock a discussion thread (via **⋯ → Lock conversation**) when:

- A Design Proposal decision has been recorded and is no longer open for input
- A Q&A thread has a marked answer and is fully resolved
- An Announcement is complete and replies are no longer needed

Locking preserves the record without inviting further responses.

### Converting Discussions to Issues

Use the **"Create issue from discussion"** button when an idea or proposal has reached enough clarity to become trackable work. This:

- Links the issue back to the originating discussion
- Keeps the discussion thread as the design context and the issue as the delivery unit
- Avoids mixing exploratory conversations with actionable tasks

### Converting Issues to Discussions

If an issue is filed prematurely (the problem is still exploratory, or no clear solution exists), convert it to a Discussion (via **⋯ → Convert to discussion**). This signals that more thinking is needed before work begins.

### Closing vs Locking

| Action | When to use |
|---|---|
| Close | The discussion reached a natural conclusion but further comment is welcome |
| Lock | The decision is final, the thread is complete, or further input is not needed |

---

## Reference

- [About GitHub Discussions](https://docs.github.com/en/discussions/collaborating-with-your-community-using-discussions/about-discussions)
- [Best practices for community conversations on GitHub](https://docs.github.com/en/discussions/guides/best-practices-for-community-conversations-on-github)
- [Quickstart for GitHub Discussions](https://docs.github.com/en/discussions/quickstart)
- [Contributing guide](../CONTRIBUTING.md)
