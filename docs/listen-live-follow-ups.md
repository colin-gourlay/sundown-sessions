# Listen Live Follow-ups

## Branded HTML5 Player

The Listen Live page currently keeps the browser-native audio controls for accessibility, browser compatibility, and low maintenance risk.

A future refinement could layer a branded custom HTML5 player interface over the existing audio element while preserving the native element in the DOM as the compatibility and accessibility fallback. That work should stay separate from metadata and layout refinements so playback behaviour can be tested carefully.
