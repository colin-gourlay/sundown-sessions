(function () {
  "use strict";

  var POLL_INTERVAL_MS = 45000;
  var FALLBACK_COPY = "Waiting for track information...";
  var MIN_SHARP_ARTWORK_DENSITY = 1.5;
  var MIN_USEFUL_ARTWORK_EDGE = 96;

  var root = document.querySelector("[data-now-playing]");
  if (!root || !window.fetch) {
    return;
  }

  var elements = {
    body: root.querySelector("[data-now-playing-state]"),
    fallback: root.querySelector("[data-now-playing-fallback]"),
    details: root.querySelector("[data-now-playing-details]"),
    artwork: root.querySelector("[data-now-playing-artwork]"),
    artworkWrap: root.querySelector("[data-now-playing-artwork-wrap]"),
    placeholder: root.querySelector("[data-now-playing-artwork-placeholder]"),
    artistRow: root.querySelector("[data-now-playing-artist-row]"),
    artist: root.querySelector("[data-now-playing-artist]"),
    trackRow: root.querySelector("[data-now-playing-track-row]"),
    track: root.querySelector("[data-now-playing-track]"),
    albumRow: root.querySelector("[data-now-playing-album-row]"),
    album: root.querySelector("[data-now-playing-album]")
  };

  var lastRendered = "";
  var transitionTimeoutId = null;

  function clean(value) {
    return typeof value === "string" ? value.replace(/\s+/g, " ").trim() : "";
  }

  function firstClean(values) {
    for (var i = 0; i < values.length; i += 1) {
      var value = clean(values[i]);
      if (value) {
        return value;
      }
    }

    return "";
  }

  function firstObject(values) {
    for (var i = 0; i < values.length; i += 1) {
      if (values[i] && typeof values[i] === "object" && !Array.isArray(values[i])) {
        return values[i];
      }
    }

    return null;
  }

  function firstArtwork(values) {
    for (var i = 0; i < values.length; i += 1) {
      var original = clean(values[i]);
      if (isUsefulUrl(original)) {
        var improved = improveArtworkUrl(original);
        return {
          url: improved,
          fallback: improved === original ? "" : original
        };
      }
    }

    return {
      url: "",
      fallback: ""
    };
  }

  function improveArtworkUrl(value) {
    if (!value) {
      return "";
    }

    try {
      var url = new URL(value, window.location.href);
      var originalPath = url.pathname;

      // Several metadata providers expose small square artwork by default even
      // when the same image is available at a larger size. Prefer the larger
      // rendition before the browser starts loading the image so the live page
      // does not upscale tiny thumbnails into the 17.5rem artwork frame.
      url.pathname = url.pathname
        .replace(/\/([0-9]{2,3})x\1bb(?=\.[a-z]+$)/i, "/600x600bb")
        .replace(/\/(34s|64s|174s)(?=\/)/i, "/500x500");

      return url.pathname === originalPath ? value : url.href;
    } catch (error) {
      return value;
    }
  }

  function isUsefulUrl(value) {
    if (!value) {
      return false;
    }

    try {
      var url = new URL(value, window.location.href);
      return url.protocol === "https:" || url.protocol === "http:";
    } catch (error) {
      return false;
    }
  }

  function splitCombinedTitle(value) {
    var title = clean(value);
    var separator = title.indexOf(" - ");

    if (!title) {
      return null;
    }

    if (separator === -1) {
      return {
        artist: "",
        track: title
      };
    }

    return {
      artist: clean(title.slice(0, separator)),
      track: clean(title.slice(separator + 3))
    };
  }

  function normalizeMetadata(source) {
    if (!source || typeof source !== "object") {
      return null;
    }

    var nested = firstObject([
      source.now_playing,
      source.nowPlaying,
      source.current,
      source.currentTrack,
      source.current_track,
      source.data
    ]);

    if (nested) {
      var nestedMetadata = normalizeMetadata(nested);
      if (nestedMetadata) {
        return nestedMetadata;
      }
    }

    var trackObject = firstObject([source.track, source.song]);
    var combinedTitle = firstClean([
      source.nowplaying,
      source.nowPlaying,
      source.currently_playing,
      source.currentlyPlaying,
      source.now_playing,
      source.title,
      source.track,
      source.song,
      source.songtitle,
      source.currentSong,
      source.text
    ]);

    var artwork = firstArtwork([
      source.artworkLarge,
      source.artwork_large,
      source.coverartLarge,
      source.coverart_large,
      source.coverArtLarge,
      source.cover_art_large,
      source.imageLarge,
      source.image_large,
      source.imageUrlLarge,
      source.image_url_large,
      source.coverart,
      source.coverArt,
      source.cover_art,
      source.artwork,
      source.artworkUrl,
      source.artwork_url,
      source.art,
      source.albumArt,
      source.album_art,
      source.cover,
      source.coverUrl,
      source.cover_url,
      source.image,
      source.imageUrl,
      source.image_url,
      trackObject && trackObject.artworkLarge,
      trackObject && trackObject.artwork_large,
      trackObject && trackObject.coverartLarge,
      trackObject && trackObject.cover_art_large,
      trackObject && trackObject.imageLarge,
      trackObject && trackObject.image_url_large,
      trackObject && trackObject.coverart,
      trackObject && trackObject.artwork,
      trackObject && trackObject.artworkUrl,
      trackObject && trackObject.albumArt,
      trackObject && trackObject.cover,
      trackObject && trackObject.image
    ]);

    var metadata = {
      artist: firstClean([
        source.artist,
        source.artistName,
        source.artist_name,
        trackObject && trackObject.artist,
        trackObject && trackObject.artistName,
        trackObject && trackObject.artist_name
      ]),
      track: firstClean([
        source.trackTitle,
        source.track_title,
        source.title,
        source.name,
        trackObject && trackObject.track,
        trackObject && trackObject.trackTitle,
        trackObject && trackObject.track_title,
        trackObject && trackObject.title,
        trackObject && trackObject.name
      ]),
      album: firstClean([
        source.album,
        source.albumTitle,
        source.album_title,
        trackObject && trackObject.album,
        trackObject && trackObject.albumTitle,
        trackObject && trackObject.album_title
      ]),
      artwork: artwork.url,
      artworkFallback: artwork.fallback
    };

    if ((!metadata.artist || !metadata.track) && combinedTitle) {
      var split = splitCombinedTitle(combinedTitle);
      if (split) {
        metadata.artist = metadata.artist || split.artist;
        metadata.track = metadata.track || split.track;
      }
    }

    if (!metadata.artist && !metadata.track && !metadata.album && !metadata.artwork) {
      return null;
    }

    return metadata;
  }

  function setText(node, value) {
    if (node) {
      node.textContent = value;
    }
  }

  function setRow(row, node, value) {
    if (!row || !node) {
      return;
    }

    row.hidden = !value;
    setText(node, value || "");
  }

  function resetArtworkPresentation() {
    if (!elements.artworkWrap) {
      return;
    }

    elements.artworkWrap.removeAttribute("data-now-playing-low-resolution");
    elements.artworkWrap.removeAttribute("data-now-playing-contain");
    elements.artworkWrap.style.removeProperty("--now-playing-artwork-width");
    elements.artworkWrap.style.removeProperty("--now-playing-artwork-height");
  }

  function showArtworkPlaceholder() {
    if (elements.artwork) {
      elements.artwork.hidden = true;
    }
    if (elements.placeholder) {
      elements.placeholder.hidden = false;
    }
  }

  function clearArtwork() {
    if (elements.artwork) {
      elements.artwork.hidden = true;
      elements.artwork.onload = null;
      elements.artwork.onerror = null;
      elements.artwork.removeAttribute("src");
      elements.artwork.removeAttribute("srcset");
      elements.artwork.alt = "";
    }

    resetArtworkPresentation();
    showArtworkPlaceholder();
  }

  function markTransition() {
    if (transitionTimeoutId) {
      window.clearTimeout(transitionTimeoutId);
    }

    if (elements.body) {
      elements.body.setAttribute("data-now-playing-transition", "");
    }
    if (elements.artworkWrap) {
      elements.artworkWrap.setAttribute("data-now-playing-transition", "");
    }

    transitionTimeoutId = window.setTimeout(function () {
      if (elements.body) {
        elements.body.removeAttribute("data-now-playing-transition");
      }
      if (elements.artworkWrap) {
        elements.artworkWrap.removeAttribute("data-now-playing-transition");
      }
    }, 320);
  }

  function renderFallback() {
    if (lastRendered === "fallback") {
      return;
    }

    lastRendered = "fallback";
    markTransition();

    if (elements.body) {
      elements.body.setAttribute("data-now-playing-state", "fallback");
    }
    if (elements.fallback) {
      elements.fallback.hidden = false;
      elements.fallback.textContent = FALLBACK_COPY;
    }
    if (elements.details) {
      elements.details.hidden = true;
    }
    setRow(elements.artistRow, elements.artist, "");
    setRow(elements.trackRow, elements.track, "");
    setRow(elements.albumRow, elements.album, "");
    clearArtwork();
  }

  function updateArtworkQualityState() {
    if (!elements.artwork || !elements.artworkWrap || !elements.artwork.naturalWidth || !elements.artwork.naturalHeight) {
      return false;
    }

    var naturalWidth = elements.artwork.naturalWidth;
    var naturalHeight = elements.artwork.naturalHeight;
    if (Math.min(naturalWidth, naturalHeight) < MIN_USEFUL_ARTWORK_EDGE) {
      resetArtworkPresentation();
      showArtworkPlaceholder();
      return false;
    }

    var renderedWidth = elements.artworkWrap.clientWidth || elements.artwork.clientWidth;
    var renderedHeight = elements.artworkWrap.clientHeight || elements.artwork.clientHeight;
    if (!renderedWidth || !renderedHeight) {
      return false;
    }

    var fitScale = Math.min(renderedWidth / naturalWidth, renderedHeight / naturalHeight);
    var displayedWidth = naturalWidth * fitScale;
    var displayedHeight = naturalHeight * fitScale;
    var availableDensity = Math.min(naturalWidth / displayedWidth, naturalHeight / displayedHeight);
    var isLowResolution = availableDensity < MIN_SHARP_ARTWORK_DENSITY;
    var isNonSquare = Math.abs((naturalWidth / naturalHeight) - 1) > 0.05;

    resetArtworkPresentation();
    elements.artworkWrap.toggleAttribute("data-now-playing-contain", isNonSquare);

    if (isLowResolution) {
      // Keep small upstream covers below the point where browser interpolation
      // makes them visibly soft. The surrounding branded frame makes the
      // intentionally inset presentation feel deliberate rather than broken.
      var sharpScale = Math.min(
        renderedWidth / naturalWidth,
        renderedHeight / naturalHeight,
        1 / MIN_SHARP_ARTWORK_DENSITY
      );
      var sharpWidth = Math.max(1, Math.floor(naturalWidth * sharpScale));
      var sharpHeight = Math.max(1, Math.floor(naturalHeight * sharpScale));

      elements.artworkWrap.style.setProperty("--now-playing-artwork-width", sharpWidth + "px");
      elements.artworkWrap.style.setProperty("--now-playing-artwork-height", sharpHeight + "px");
      elements.artworkWrap.setAttribute("data-now-playing-low-resolution", "");
    }

    elements.artwork.hidden = false;
    if (elements.placeholder) {
      elements.placeholder.hidden = true;
    }

    return true;
  }

  function loadArtwork(metadata) {
    if (!metadata.artwork || !elements.artwork) {
      clearArtwork();
      return;
    }

    resetArtworkPresentation();
    showArtworkPlaceholder();

    var fallbackAttempted = false;
    elements.artwork.alt = "Album artwork for " + (metadata.track || "the current track") + (metadata.artist ? " by " + metadata.artist : "");
    elements.artwork.onload = function () {
      updateArtworkQualityState();
    };
    elements.artwork.onerror = function () {
      if (!fallbackAttempted && metadata.artworkFallback) {
        fallbackAttempted = true;
        resetArtworkPresentation();
        showArtworkPlaceholder();
        elements.artwork.src = metadata.artworkFallback;
        return;
      }

      clearArtwork();
      // Permit the next metadata poll to retry after a transient image or
      // network failure even when the artist and track have not changed.
      lastRendered = "";
    };
    elements.artwork.src = metadata.artwork;
  }

  function renderMetadata(metadata) {
    var nextKey = JSON.stringify(metadata);
    if (nextKey === lastRendered) {
      return;
    }

    lastRendered = nextKey;
    markTransition();

    if (elements.body) {
      elements.body.setAttribute("data-now-playing-state", "ready");
    }
    if (elements.fallback) {
      elements.fallback.hidden = true;
    }
    if (elements.details) {
      elements.details.hidden = false;
    }

    setRow(elements.artistRow, elements.artist, metadata.artist);
    setRow(elements.trackRow, elements.track, metadata.track);
    setRow(elements.albumRow, elements.album, metadata.album);

    loadArtwork(metadata);
  }

  function logQuietly(error) {
    if (window.location.hostname === "localhost" || window.location.hostname === "127.0.0.1") {
      console.debug("Listen Live metadata unavailable", error);
    }
  }

  function refresh() {
    var endpoint = root.getAttribute("data-now-playing-json");
    if (!endpoint) {
      renderFallback();
      return;
    }

    // The NuCast endpoint returns the required fields as JSON. If a deployed
    // browser is denied by CORS, static hosting cannot safely work around it;
    // keep the existing fallback and use a future station API/proxy if needed.
    fetch(endpoint, { cache: "no-store", mode: "cors" })
      .then(function (response) {
        if (!response.ok) {
          throw new Error("Metadata JSON returned " + response.status);
        }

        return response.json();
      })
      .then(normalizeMetadata)
      .then(function (metadata) {
        if (metadata && (metadata.artist || metadata.track || metadata.album)) {
          renderMetadata(metadata);
        } else {
          renderFallback();
        }
      })
      .catch(function (error) {
        logQuietly(error);
        renderFallback();
      });
  }

  renderFallback();
  refresh();
  window.setInterval(refresh, POLL_INTERVAL_MS);
  window.addEventListener("resize", function () {
    if (elements.artwork && !elements.artwork.hidden) {
      updateArtworkQualityState();
    }
  });
}());
