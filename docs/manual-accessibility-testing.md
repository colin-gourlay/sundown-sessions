# Manual Accessibility Testing

Use this procedure alongside the [WCAG 2.2 AA accessibility standard](accessibility.md).
It is intended to make useful manual checks routine without turning every pull
request into a full-site audit.

## Choose proportionate coverage

For an ordinary user-facing pull request, test the pages, components and journeys
changed by the pull request. Include one representative page for each affected
template and check the shared controls encountered on the way. The complete
representative-site baseline is not required for every pull request.

Broaden the regression coverage when a change affects:

- site navigation, search, shared layouts, the footer or theme behaviour;
- reusable form controls, validation or dynamic status messages;
- dialogs, focus management or global keyboard behaviour;
- media controls or listen-live state handling;
- typography, spacing or colours used across multiple sections; or
- the Hugo theme, accessibility tooling or other cross-site foundations.

For those changes, test every affected journey and a representative page from
each dependent section. Run the full baseline for a major theme or structural
change, or as part of a deliberate accessibility review.

## Prepare the site

Build and serve the production site using the same shape as the Accessibility
workflow:

```bash
hugo \
  --source src \
  --environment production \
  --baseURL http://127.0.0.1:1313/
python3 -m http.server 1313 --bind 127.0.0.1 --directory ./src/public
```

In another terminal, run the configured automated checks:

```bash
npx --yes pa11y-ci --config .pa11yci.json
```

Automated results inform the review but do not replace the checks below.

## Check each affected journey

1. Use only the keyboard. Start at the address bar, move through the page in
   both directions, activate controls and complete the journey. Confirm that
   the order is logical, focus is always visible and no control traps focus.
2. Exercise each changed state. Include menus, dialogs, search results, form
   validation, success and error messages, media controls and loading or empty
   states where relevant. Confirm dialogs contain focus, close with
   <kbd>Escape</kbd> and return focus to their opener.
3. With an available screen reader, check the page title, landmarks and heading
   structure, then spot-check the changed content and interactions. Confirm
   controls have useful names, state changes are announced once, form guidance
   is associated with its field and reading order remains sensible.
4. At 200% browser zoom, complete the journey without losing content or
   functionality. Where practical, repeat at 400%. Check a 320 CSS-pixel-wide
   viewport for reflow and make sure two-dimensional scrolling is only used
   where the content genuinely requires it.
5. Inspect new or changed colour combinations, including hover, focus, disabled,
   error and dark-theme states. Confirm information is not conveyed by colour
   alone.

Use real submissions or live media only when it is safe to do so. Otherwise,
exercise validation and status behaviour with a test endpoint or a mocked
response, and record the limitation.

## Record the result

Add a short section like this to the pull request description:

```markdown
## Accessibility verification

- Scope: [affected pages, components and journeys]
- Environment: [browser/version, viewport, operating system, screen reader/version]
- Keyboard and focus: [pass/fail and interactions checked]
- Screen reader: [pass/fail and interactions checked]
- Zoom and reflow: [200%, 400% if practical, and narrow viewport result]
- Automated check: [command and result]
- Limitations or follow-ups: [not tested, reason, and issue links]
```

Record genuine defects as focused issues with reproduction steps, affected
controls, expected behaviour and test environment. Link them from the pull
request rather than expanding an unrelated change into a general remediation.

## Full representative baseline

The full baseline covers:

- homepage and primary navigation;
- search modal and search results;
- shows index and an individual published show;
- artists and releases indexes;
- listen-live and its media and status controls;
- contact and corrections forms; and
- about and 404 pages.

The latest full result is the
[3 September 2026 accessibility baseline](../reports/accessibility-baseline-2026-09-03.md).
