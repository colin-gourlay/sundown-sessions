# Listen Live Artwork Quality

## Investigation

The NuCast now-playing response was inspected on 15 July 2026 from the same
public endpoint used by the Listen Live page. The response supplied a local
NuCast cover URL whose intrinsic image dimensions were 185 by 185 pixels. The
endpoint did not expose a separate large-artwork field, and requesting common
larger URL variants returned either the same 185-pixel asset or a missing
resource.

The Listen Live artwork frame can render at 280 by 280 CSS pixels. The source
image was therefore being enlarged to roughly 151% of its intrinsic size on a
standard-density display, with a larger effective deficit on high-density
screens. This browser interpolation was the cause of the visible softness.
Changing `object-fit` alone could not improve the source image detail.

## Rendering Behaviour

The now-playing script continues to prefer explicit large-artwork metadata and
known higher-resolution renditions when the upstream URL supports them. Once an
image loads, the script compares its intrinsic dimensions with the available
artwork frame.

- Adequately sized square artwork fills the established square frame.
- Non-square artwork uses `object-fit: contain` so it keeps its proportions and
  is not cropped or stretched.
- Low-resolution artwork is displayed as an intentional inset within the
  branded frame. Its rendered dimensions are capped using the intrinsic image
  size and a 1.5 source-pixel density target, rather than allowing the browser
  to stretch it to 280 pixels.
- Artwork with an edge below 96 pixels, artwork that fails to load, and missing
  artwork use the existing branded Sundown Sessions fallback.
- If a higher-resolution URL variant fails, the original artwork URL is tried
  before the fallback is shown. Unrecoverable failures are retried on the next
  metadata poll so a transient network problem does not hide artwork for the
  rest of the track.

This remains a client-side enhancement with no image-processing dependency,
proxy, persisted third-party data, or change to audio playback and metadata
polling.

## Validation Notes

The implementation can be checked off-air with captured metadata and images at
representative square, portrait, landscape, tiny, and missing-image sizes. A
listener-facing check during a live Tuesday broadcast is still required after
deployment because the precise cover supplied by NuCast varies by track.
