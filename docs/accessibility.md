# Accessibility Standard

Sundown Sessions targets **WCAG 2.2 Level AA** as the minimum accessibility standard for the public website.

Accessibility is a core quality attribute of the site. It should be considered during design, development, content creation, review, testing, and future maintenance rather than treated as a one-off remediation exercise.

## Scope

The standard applies to all public-facing pages, templates, components, and content, including:

- Homepage and section listing pages.
- Show, artist, release, track, about, contact, search, and listen-live pages.
- Header navigation, footer navigation, menus, forms, media players, theme switching, search, and other interactive elements.
- Images, artwork, icons, embedded media, and editorial content.

## Development expectations

All changes should preserve or improve accessibility by default. Contributors should:

- Use semantic HTML before adding ARIA.
- Provide meaningful text alternatives for informative images.
- Mark decorative images and icons so they are ignored by assistive technologies.
- Keep link and button text descriptive when read out of context.
- Ensure all interactive controls have accessible names.
- Preserve visible focus indicators and logical focus order.
- Avoid keyboard traps and ensure all functionality can be operated with a keyboard.
- Maintain sufficient colour contrast for text, controls, focus states, and meaningful visual indicators.
- Support responsive reflow and text zoom without loss of content or functionality.
- Provide labels, instructions, and status messages for forms and dynamic interactions.
- Prefer predictable behaviour and plain, concise language for user-facing copy.

## Review checklist

Before opening or merging a pull request that changes public-facing output, review the affected pages against this checklist:

### Perceivable

- Images have appropriate `alt` text, or empty `alt` text when decorative.
- Icons used as controls are accompanied by accessible labels.
- Text and UI states meet WCAG AA contrast expectations.
- Content remains readable when text is resized and at narrow viewports.
- Audio, media, and live-stream affordances include equivalent text labels or instructions where practical.

### Operable

- Links, buttons, menus, forms, and media controls are reachable and usable with the keyboard.
- Focus order follows the visual and document order.
- Focus is visible on custom links and controls.
- Skip links, navigation, and landmark structure remain usable.
- Touch targets are large enough for comfortable activation where custom controls are introduced.

### Understandable

- Navigation labels are consistent across desktop and mobile views.
- Forms provide clear labels, instructions, and status messages.
- Error and success messages are announced where dynamic behaviour is used.
- User-facing copy uses British English and avoids unnecessary jargon.

### Robust

- HTML is valid and uses landmarks, headings, lists, buttons, and links for their intended purposes.
- ARIA is only used when native HTML is insufficient.
- ARIA attributes remain synchronised with visible state.
- Dynamic components remain understandable to screen readers and other assistive technologies.

## Automated validation

Automated checks cannot prove full WCAG conformance, but they help catch regressions early. The repository includes an accessibility workflow that builds the Hugo site and runs `pa11y-ci` against representative pages on pull requests and pushes that affect the website.

When changing templates, layout, navigation, forms, or interactive behaviour, run an equivalent local accessibility check where possible and document any limitations in the pull request.

## Manual validation

Manual testing is required for meaningful accessibility confidence. For substantial user-facing changes, validate the affected journeys using:

- Keyboard-only navigation.
- Browser zoom and responsive reflow checks at 200% and, where practical, up to 400%.
- Colour contrast inspection for new colour combinations.
- Screen reader spot checks with available assistive technology, such as NVDA, VoiceOver, or Narrator.

Document the pages and journeys tested in the pull request. If a check cannot be completed locally, call out the limitation and any follow-up needed.
