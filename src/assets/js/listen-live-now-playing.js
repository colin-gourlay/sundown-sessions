(function () {
  "use strict";

  var POLL_INTERVAL_MS = 45000;
  var FALLBACK_COPY = "Live track information is not available just yet. Press play to hear what's currently drifting through Sundown Sessions.";

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

  function getEndpoint(name) {
    return root.getAttribute(name) || "";
  }

  function clean(value) {
    return typeof value === "string" ? value.replace(/\s+/g, " ").trim() : "";
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
    if (!title) {
      return null;
    }

    var separator = title.indexOf(" - ");
    if (separator === -1) {
      return {
        artist: "",
        track: title,
        album: "",
        artwork: ""
      };
    }

    return {
      artist: clean(title.slice(0, separator)),
      track: clean(title.slice(separator + 3)),
      album: "",
      artwork: ""
    };
  }

  function normalizeMetadata(source) {
    if (!source) {
      return null;
    }

    var combinedTitle = clean(source.title || source.nowplaying || source.songtitle || source.currentSong || source.text);
    var metadata = {
      artist: clean(source.artist || source.artistName),
      track: clean(source.track || source.title || source.song || source.name),
      album: clean(source.album || source.albumTitle),
      artwork: clean(source.artwork || source.artworkUrl || source.cover || source.coverUrl || source.image)
    };

    if ((!metadata.artist || !metadata.track) && combinedTitle) {
      var split = splitCombinedTitle(combinedTitle);
      if (split) {
        metadata.artist = metadata.artist || split.artist;
        metadata.track = metadata.track || split.track;
      }
    }

    if (!metadata.track && combinedTitle) {
      metadata.track = combinedTitle;
    }

    if (!metadata.artist && !metadata.track && !metadata.album && !metadata.artwork) {
      return null;
    }

    if (!isUsefulUrl(metadata.artwork)) {
      metadata.artwork = "";
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

  function renderFallback() {
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
    if (elements.artworkWrap) {
      elements.artworkWrap.setAttribute("aria-hidden", "true");
    }
  }

  function renderMetadata(metadata) {
    var nextKey = JSON.stringify(metadata);
    if (nextKey === lastRendered) {
      return;
    }
    lastRendered = nextKey;

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
      if (elements.artworkWrap) {
        elements.artworkWrap.removeAttribute("aria-hidden");
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
      if (elements.artworkWrap) {
        elements.artworkWrap.setAttribute("aria-hidden", "true");
      }
    }
  }

  function logQuietly(error) {
    if (window.location.hostname === "localhost" || window.location.hostname === "127.0.0.1") {
      console.debug("Listen Live metadata unavailable", error);
    }
  }

  function fetchJsonEndpoint(url) {
    if (!url) {
      return Promise.resolve(null);
    }

    // This must be a same-origin URL or an endpoint that explicitly allows
    // https://sundownsessions.co.uk with Access-Control-Allow-Origin.
    // NuCast's public JSON and Shoutcast status URLs currently do not send
    // those CORS headers, so they should not be called directly from GitHub Pages.
    return fetch(url, { cache: "no-store", mode: "cors" })
      .then(function (response) {
        if (!response.ok) {
          throw new Error("Metadata JSON returned " + response.status);
        }
        return response.json();
      })
      .then(normalizeMetadata);
  }

  function refresh() {
    var endpoint = getEndpoint("data-now-playing-endpoint");
    if (!endpoint) {
      renderFallback();
      return Promise.resolve();
    }

    return fetchJsonEndpoint(endpoint).catch(function (error) {
      logQuietly(error);
      return null;
    }).then(function (metadata) {
      if (metadata) {
        renderMetadata(metadata);
      } else {
        renderFallback();
      }
    });
  }

  renderFallback();
  refresh();
  if (getEndpoint("data-now-playing-endpoint")) {
    window.setInterval(refresh, POLL_INTERVAL_MS);
  }
}());
