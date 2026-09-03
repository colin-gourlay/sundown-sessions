# Accessibility Baseline — 3 September 2026

## Outcome

The representative site was checked against the repository's WCAG 2.2 AA
standard. The configured Pa11y suite reported zero errors across all 12 URLs.
Keyboard, focus, accessibility-tree, screen-reader, form, dialog, media-status
and reflow checks found three focused follow-ups; no issue prevented the rest of
the baseline from being completed.

## Environment and method

- Ubuntu 24.04.4 LTS.
- Google Chrome 152.0.7977.64 at a 1280 × 800 CSS-pixel desktop viewport.
- Reflow checks at 640 × 512 and 320 × 256 CSS pixels, representing the
  available layout at 200% and 400% zoom from a 1280-pixel viewport.
- Orca 46.1 with Chrome accessibility enabled. Page summaries and spoken output
  were inspected from Orca's debug output; Chrome's accessibility tree was also
  inspected for every journey.
- Production Hugo build with Hugo 0.165.0 extended, served locally over HTTP.

The keyboard pass traversed the rendered pages in DOM order and checked the
computed focus treatment of interactive elements. Search, forms and player
status behaviour were exercised separately rather than inferred from static
markup.

## Automated result

The following command passed all 12 configured URLs with zero Pa11y errors:

```bash
npx --yes pa11y-ci --config .pa11yci.json
```

The run revealed a coverage problem rather than a reported violation:
`/shows/1/` serves an asset directory listing in this local static-server setup,
not the canonical show page. [Issue #917](https://github.com/colin-gourlay/sundown-sessions/issues/917)
tracks correction of that URL. The individual-show manual checks below used
`/shows/featuring-the-big-now/`.

## Representative journeys

| Journey | Keyboard and focus | Screen-reader and semantics | 200% / 400% reflow | Result |
| --- | --- | --- | --- | --- |
| Homepage and primary navigation | Logical desktop order; visible focus | Named document, navigation and main landmarks announced | No page-level horizontal overflow | Pass |
| Search modal and results | Input and arrow-key result navigation work; modal focus defects found | Input has a label, but the overlay lacks dialog semantics | No page-level horizontal overflow | Follow-up #916 |
| Shows index | Links and pagination reachable with visible focus | Heading and landmark structure exposed | No page-level horizontal overflow | Pass |
| Individual show | Long-form controls and links reachable with visible focus | Named document, navigation and main landmarks exposed | No page-level horizontal overflow | Pass |
| Artists index | Alphabet navigation and artist links reachable with visible focus | List and navigation structure exposed | No page-level horizontal overflow | Pass |
| Releases index | Discovery, facets and release links reachable with visible focus | Heading and landmark structure exposed | No page-level horizontal overflow | Pass |
| Listen-live | Player and surrounding links keyboard reachable | Player named “Sundown Sessions live stream”; polite status exposed | No page-level horizontal overflow | Pass |
| Contact form | Labels, required-field focus and submit control verified | Field guidance associated; mocked failure produced an alert | No page-level horizontal overflow | Pass |
| Corrections form | Labels, required-field focus and submit control verified | Field guidance associated; mocked failure produced an alert | No page-level horizontal overflow | Pass |
| About | Links follow a logical order with visible focus | Named document, navigation and main landmarks exposed | No page-level horizontal overflow | Pass |
| 404 | Recovery actions keyboard reachable with visible focus | Page title, headings and landmarks exposed | No page-level horizontal overflow | Pass |
| Mobile primary navigation | Visible trigger is not in the tab order at 320 pixels | Modal menu cannot be opened by keyboard for a complete check | Layout itself does not overflow | Follow-up #918 |

## Interaction details

Search opened with focus in the labelled query field. A query returned results,
and the arrow keys moved focus into the result list. The overlay does not expose
dialog semantics, does not contain focus and leaves focus on the document body
when closed. The keyboard-shortcuts help dialog has the same containment and
return-focus problem. These defects are tracked in
[issue #916](https://github.com/colin-gourlay/sundown-sessions/issues/916).

At the mobile breakpoint, the navigation opener is a non-focusable label for a
hidden checkbox. It cannot be reached or operated by keyboard. Focus management
for the resulting modal menu is therefore also incomplete. This is tracked in
[issue #918](https://github.com/colin-gourlay/sundown-sessions/issues/918).

Both forms moved focus to their first invalid required control. All user-facing
fields had labels and supporting guidance was programmatically associated where
present. A mocked failed submission produced a named dismiss control and an
assertive error alert; buttons returned from their busy and disabled states.
No real Formspree submission was sent.

The listen-live player exposed an accessible name and polite atomic status. Its
status changed from the initial instruction to connecting and playing messages
when the corresponding media events were exercised. The external audio stream
was not played end to end, so stream availability and native player behaviour
outside those states were not assessed.

## Limitations

- Orca ran with a virtual display. Its spoken page summaries were inspected from
  debug output, while detailed control names, roles, relationships and live
  regions were spot-checked in Chrome's accessibility tree rather than through
  a conventional desktop audio session.
- The 200% and 400% checks used the equivalent available CSS viewport widths.
  Browser-chrome zoom controls were not available in the automated browser
  session.
- Form submission failure and media state changes were exercised with mocked
  responses and events. No external form data was sent, and stream playback was
  not used to judge the availability of the third-party services.

## Follow-up defects

- [#916 — Make modal focus management keyboard accessible](https://github.com/colin-gourlay/sundown-sessions/issues/916)
- [#917 — Audit a canonical show page in the Pa11y suite](https://github.com/colin-gourlay/sundown-sessions/issues/917)
- [#918 — Make the mobile navigation menu keyboard operable](https://github.com/colin-gourlay/sundown-sessions/issues/918)

Future user-facing pull requests should use the
[manual accessibility testing procedure](../docs/manual-accessibility-testing.md)
and normally limit coverage to their affected pages, components and journeys.
