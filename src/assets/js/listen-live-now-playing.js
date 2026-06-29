(function () {
  "use strict";

  var POLL_INTERVAL_MS = 45000;
  var STREAM_TIMEOUT_MS = 10000;
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

  function getEndpoint(name) {
    return root.getAttribute(name) || "";
  }

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

    var richNested = firstObject([
      source.now_playing,
      source.nowPlaying,
      source.current,
      source.currentTrack,
      source.data
    ]);

    if (richNested) {
      var nestedMetadata = normalizeMetadata(richNested);
      if (nestedMetadata) {
        return nestedMetadata;
      }
    }

    var trackObject = firstObject([source.track, source.song]);
    var combinedTitle = firstClean([
      source.nowplaying,
      source.now_playing,
      source.title,
      source.songtitle,
      source.currentSong,
      source.text
    ]);
    var metadata = {
      artist: firstClean([source.artist, source.artistName, source.artist_name, trackObject && trackObject.artist, trackObject && trackObject.artistName]),
      track: firstClean([source.track, source.trackTitle, source.track_title, source.title, source.song, source.name, trackObject && trackObject.track, trackObject && trackObject.title, trackObject && trackObject.name]),
      album: firstClean([source.album, source.albumTitle, source.album_title, trackObject && trackObject.album, trackObject && trackObject.albumTitle]),
      artwork: firstClean([
        source.artwork,
        source.artworkUrl,
        source.artwork_url,
        source.albumArt,
        source.album_art,
        source.cover,
        source.coverUrl,
        source.cover_url,
        source.image,
        source.imageUrl,
        source.image_url,
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

  function parseShoutcastSevenHtml(html) {
    var text = clean(html.replace(/<[^>]+>/g, " "));
    var fields = text.split(",");
    if (fields.length < 7) {
      return null;
    }

    return normalizeMetadata({
      title: fields.slice(6).join(",")
    });
  }

  function parseIcyStreamTitle(value) {
    var match = value.match(/StreamTitle='([^']*)'/i);
    return match ? normalizeMetadata({ title: match[1] }) : null;
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
    if (elements.artworkWrap) {
      elements.artworkWrap.removeAttribute("aria-hidden");
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
        elements.artworkWrap.removeAttribute("aria-hidden");
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

    // NuCast currently exposes useful JSON here, but does not send CORS headers.
    // Static hosting cannot proxy it, so production browsers may reject this request.
    return fetch(url, { cache: "no-store", mode: "cors" })
      .then(function (response) {
        if (!response.ok) {
          throw new Error("Metadata JSON returned " + response.status);
        }
        return response.json();
      })
      .then(normalizeMetadata);
  }

  function fetchShoutcastEndpoint(url) {
    if (!url) {
      return Promise.resolve(null);
    }

    // Shoutcast /7.html returns a comma-delimited title, but it may also be
    // blocked by CORS when requested from GitHub Pages.
    return fetch(url, { cache: "no-store", mode: "cors" })
      .then(function (response) {
        if (!response.ok) {
          throw new Error("Shoutcast metadata returned " + response.status);
        }
        return response.text();
      })
      .then(parseShoutcastSevenHtml);
  }

  function fetchIcyStreamMetadata(url) {
    if (!url || !window.ReadableStream || !window.TextDecoder) {
      return Promise.resolve(null);
    }

    var controller = new AbortController();
    var timeoutId = window.setTimeout(function () {
      controller.abort();
    }, STREAM_TIMEOUT_MS);

    // This stream does send CORS headers, but reading ICY metadata needs the
    // Icy-MetaData request header. Some browsers or servers will reject the
    // resulting preflight, so this remains a best-effort fallback.
    return fetch(url, {
      cache: "no-store",
      headers: {
        "Icy-MetaData": "1"
      },
      mode: "cors",
      signal: controller.signal
    })
      .then(function (response) {
        var metaint = parseInt(response.headers.get("icy-metaint"), 10);
        if (!response.ok || !response.body || !metaint) {
          throw new Error("ICY stream metadata is not readable");
        }

        var reader = response.body.getReader();
        var decoder = new TextDecoder("utf-8");
        var bytesSeen = 0;
        var metadataLength = null;
        var metadataBytes = [];

        function readChunk() {
          return reader.read().then(function (result) {
            if (result.done) {
              return null;
            }

            for (var i = 0; i < result.value.length; i += 1) {
              var byte = result.value[i];
              if (bytesSeen < metaint) {
                bytesSeen += 1;
                continue;
              }

              if (metadataLength === null) {
                metadataLength = byte * 16;
                if (metadataLength === 0) {
                  controller.abort();
                  return null;
                }
                continue;
              }

              metadataBytes.push(byte);
              if (metadataBytes.length >= metadataLength) {
                controller.abort();
                return parseIcyStreamTitle(decoder.decode(new Uint8Array(metadataBytes)));
              }
            }

            return readChunk();
          });
        }

        return readChunk();
      })
      .finally(function () {
        window.clearTimeout(timeoutId);
      });
  }

  function firstSuccessful(tasks) {
    return tasks.reduce(function (promise, task) {
      return promise.then(function (metadata) {
        if (metadata) {
          return metadata;
        }

        return task().catch(function (error) {
          logQuietly(error);
          return null;
        });
      });
    }, Promise.resolve(null));
  }

  function refresh() {
    return firstSuccessful([
      function () { return fetchJsonEndpoint(getEndpoint("data-now-playing-public-json")); },
      function () { return fetchShoutcastEndpoint(getEndpoint("data-now-playing-shoutcast")); },
      function () { return fetchIcyStreamMetadata(getEndpoint("data-now-playing-stream")); }
    ]).then(function (metadata) {
      if (metadata) {
        renderMetadata(metadata);
      } else {
        renderFallback();
      }
    });
  }

  renderFallback();
  refresh();
  window.setInterval(refresh, POLL_INTERVAL_MS);
}());
