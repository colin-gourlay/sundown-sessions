(function () {
  "use strict";

  var POLL_INTERVAL_MS = 45000;
  var FALLBACK_COPY = "Waiting for track information...";

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

  function firstUrl(values) {
    for (var i = 0; i < values.length; i += 1) {
      var value = clean(values[i]);
      if (isUsefulUrl(value)) {
        return value;
      }
    }

    return "";
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
      artwork: firstUrl([
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
        trackObject && trackObject.coverart,
        trackObject && trackObject.artwork,
        trackObject && trackObject.artworkUrl,
        trackObject && trackObject.albumArt,
        trackObject && trackObject.cover,
        trackObject && trackObject.image
      ])
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
    setText(node, value);
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
    if (elements.artwork) {
      elements.artwork.hidden = true;
      elements.artwork.removeAttribute("src");
      elements.artwork.alt = "";
    }
    if (elements.placeholder) {
      elements.placeholder.hidden = false;
    }
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

    if (metadata.artwork && elements.artwork) {
      elements.artwork.src = metadata.artwork;
      elements.artwork.alt = "Album artwork for " + (metadata.track || "the current track") + (metadata.artist ? " by " + metadata.artist : "");
      elements.artwork.hidden = false;
      if (elements.placeholder) {
        elements.placeholder.hidden = true;
      }
    } else {
      if (elements.artwork) {
        elements.artwork.hidden = true;
        elements.artwork.removeAttribute("src");
        elements.artwork.alt = "";
      }
      if (elements.placeholder) {
        elements.placeholder.hidden = false;
      }
    }
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
}());
