(function (root, factory) {
  "use strict";

  if (typeof module === "object" && module.exports) {
    module.exports = factory;
  } else if (root && root.document) {
    factory(root).init();
  }
}(typeof window !== "undefined" ? window : this, function (windowObject) {
  "use strict";

  var DEFAULT_RETRY_DELAYS_MS = [1000, 2000, 5000, 10000, 20000, 30000];
  var DEFAULT_BUFFER_GRACE_MS = 12000;
  var DEFAULT_CONNECT_TIMEOUT_MS = 20000;
  var DEFAULT_PROGRESS_INTERVAL_MS = 5000;
  var DEFAULT_PROGRESS_STALL_MS = 20000;
  var DEFAULT_STABLE_PLAYBACK_MS = 30000;

  function optionNumber(options, name, fallback) {
    return options && typeof options[name] === "number" ? options[name] : fallback;
  }

  function createController(rootElement, options) {
    var settings = options || {};
    var clock = settings.clock || windowObject;
    var now = settings.now || function () {
      return Date.now();
    };
    var retryDelays = settings.retryDelaysMs || DEFAULT_RETRY_DELAYS_MS;
    var bufferGraceMs = optionNumber(settings, "bufferGraceMs", DEFAULT_BUFFER_GRACE_MS);
    var connectTimeoutMs = optionNumber(settings, "connectTimeoutMs", DEFAULT_CONNECT_TIMEOUT_MS);
    var progressIntervalMs = optionNumber(settings, "progressIntervalMs", DEFAULT_PROGRESS_INTERVAL_MS);
    var progressStallMs = optionNumber(settings, "progressStallMs", DEFAULT_PROGRESS_STALL_MS);
    var stablePlaybackMs = optionNumber(settings, "stablePlaybackMs", DEFAULT_STABLE_PLAYBACK_MS);
    var documentObject = windowObject.document;
    var navigatorObject = windowObject.navigator || {};
    var audio = rootElement && rootElement.querySelector("[data-listen-live-player-audio]");
    var source = audio && audio.querySelector("source");
    var status = rootElement && rootElement.querySelector("[data-listen-live-player-status]");
    var retryButton = rootElement && rootElement.querySelector("[data-listen-live-player-retry]");
    var canonicalStreamUrl = rootElement && rootElement.getAttribute("data-stream-url");

    var wantsPlayback = false;
    var interactionRequired = false;
    var internalReload = false;
    var suspended = false;
    var generation = 0;
    var retryIndex = 0;
    var retryTimerId = null;
    var bufferTimerId = null;
    var connectTimerId = null;
    var progressTimerId = null;
    var stableTimerId = null;
    var lastMediaTime = null;
    var noProgressSince = null;
    var hasObservedProgress = false;
    var lastProgressAt = null;
    var bufferStartMediaTime = null;
    var currentState = "";
    var listeners = [];

    if (!audio || !status || !canonicalStreamUrl) {
      return null;
    }

    function addListener(target, eventName, handler) {
      if (!target || !target.addEventListener) {
        return;
      }

      target.addEventListener(eventName, handler);
      listeners.push({
        target: target,
        eventName: eventName,
        handler: handler
      });
    }

    function clearTimer(timerName) {
      var timerId;

      if (timerName === "retry") {
        timerId = retryTimerId;
        retryTimerId = null;
      } else if (timerName === "buffer") {
        timerId = bufferTimerId;
        bufferTimerId = null;
      } else if (timerName === "connect") {
        timerId = connectTimerId;
        connectTimerId = null;
      } else if (timerName === "progress") {
        timerId = progressTimerId;
        progressTimerId = null;
      } else {
        timerId = stableTimerId;
        stableTimerId = null;
      }

      if (timerId !== null) {
        if (timerName === "progress") {
          clock.clearInterval(timerId);
        } else {
          clock.clearTimeout(timerId);
        }
      }
    }

    function resetProgressWatchdog() {
      clearTimer("progress");
      lastMediaTime = null;
      noProgressSince = null;
      hasObservedProgress = false;
      lastProgressAt = null;
    }

    function clearRecoveryTimers() {
      clearTimer("retry");
      clearTimer("buffer");
      clearTimer("connect");
      clearTimer("stable");
      resetProgressWatchdog();
    }

    function setRetryButton(show, label) {
      if (!retryButton) {
        return;
      }

      if (show) {
        retryButton.textContent = label || "Resume live stream";
        retryButton.hidden = false;
        return;
      }

      if (documentObject.activeElement === retryButton && typeof audio.focus === "function") {
        audio.focus();
      }
      retryButton.hidden = true;
    }

    function setState(state, message, showRetry, retryLabel) {
      rootElement.setAttribute("data-player-state", state);

      if (currentState !== state || status.textContent !== message) {
        status.textContent = message;
        currentState = state;
      }

      setRetryButton(Boolean(showRetry), retryLabel);
    }

    function isOffline() {
      return navigatorObject.onLine === false;
    }

    function showOffline() {
      setState(
        "offline",
        "You’re offline. We’ll reconnect when your internet connection returns.",
        false
      );
    }

    function showInteractionRequired() {
      setState(
        "interaction-required",
        "Your browser needs you to restart playback. Select Resume live stream to continue.",
        true,
        "Resume live stream"
      );
    }

    function makeReconnectUrl() {
      var separator;

      if (windowObject.URL) {
        try {
          var url = new windowObject.URL(canonicalStreamUrl, windowObject.location && windowObject.location.href);
          url.searchParams.set("ss-reconnect", String(now()) + "-" + String(generation));
          return url.href;
        } catch (error) {
          // Fall through to the string form for older browsers.
        }
      }

      separator = canonicalStreamUrl.indexOf("?") === -1 ? "?" : "&";
      return canonicalStreamUrl + separator + "ss-reconnect=" + encodeURIComponent(String(now()) + "-" + String(generation));
    }

    function stopPlaybackIntent() {
      wantsPlayback = false;
      interactionRequired = false;
      internalReload = false;
      suspended = false;
      retryIndex = 0;
      generation += 1;
      clearRecoveryTimers();
      setState("idle", "Press play to listen live.", false);
    }

    function handlePlayFailure(error, attemptGeneration) {
      var errorName;

      if (attemptGeneration !== generation || !wantsPlayback) {
        return;
      }

      internalReload = false;
      clearTimer("connect");
      errorName = error && error.name ? error.name : "";

      if (errorName === "NotAllowedError") {
        interactionRequired = true;
        clearRecoveryTimers();
        showInteractionRequired();
        return;
      }

      scheduleRecovery(false);
    }

    function startConnectTimeout(attemptGeneration) {
      clearTimer("connect");
      connectTimerId = clock.setTimeout(function () {
        connectTimerId = null;

        if (attemptGeneration !== generation || !wantsPlayback) {
          return;
        }

        internalReload = false;
        scheduleRecovery(false);
      }, connectTimeoutMs);
    }

    function attemptRecovery(showConnectingState) {
      var attemptGeneration;
      var playResult;
      var reconnectUrl;

      clearTimer("retry");

      if (!wantsPlayback || interactionRequired) {
        return;
      }

      if (isOffline()) {
        showOffline();
        return;
      }

      generation += 1;
      attemptGeneration = generation;
      internalReload = true;
      reconnectUrl = makeReconnectUrl();
      if (showConnectingState) {
        setState("connecting", "Connecting to the live stream…", false);
      }

      try {
        if (source) {
          source.setAttribute("src", reconnectUrl);
        } else {
          audio.setAttribute("src", reconnectUrl);
        }

        audio.load();
        playResult = audio.play();
      } catch (error) {
        internalReload = false;
        handlePlayFailure(error, attemptGeneration);
        return;
      }

      startConnectTimeout(attemptGeneration);

      if (playResult && typeof playResult.then === "function") {
        playResult.then(function () {
          if (attemptGeneration === generation) {
            internalReload = false;
          }
        }, function (error) {
          handlePlayFailure(error, attemptGeneration);
        });
      } else {
        clock.setTimeout(function () {
          if (attemptGeneration === generation) {
            internalReload = false;
          }
        }, 0);
      }
    }

    function scheduleRecovery(immediate, message) {
      var delayMs;

      if (!wantsPlayback || interactionRequired || suspended) {
        return;
      }

      if (isOffline()) {
        generation += 1;
        internalReload = false;
        clearRecoveryTimers();
        showOffline();
        return;
      }

      if (retryTimerId !== null) {
        return;
      }

      generation += 1;
      internalReload = false;
      clearTimer("buffer");
      clearTimer("connect");
      clearTimer("stable");
      resetProgressWatchdog();

      if (immediate) {
        delayMs = 0;
      } else {
        delayMs = retryDelays[Math.min(retryIndex, retryDelays.length - 1)];
        retryIndex += 1;
      }

      setState(
        "retrying",
        message || "Connection interrupted. Reconnecting automatically…",
        false
      );
      retryTimerId = clock.setTimeout(function () {
        retryTimerId = null;
        attemptRecovery(false);
      }, delayMs);
    }

    function startBufferGrace() {
      if (!wantsPlayback || interactionRequired || suspended || bufferTimerId !== null) {
        return;
      }

      if (isOffline()) {
        showOffline();
        return;
      }

      bufferStartMediaTime = Number.isFinite(Number(audio.currentTime)) ? Number(audio.currentTime) : null;
      bufferTimerId = clock.setTimeout(function () {
        var currentMediaTime = Number(audio.currentTime);

        bufferTimerId = null;
        if (bufferStartMediaTime !== null && Number.isFinite(currentMediaTime) &&
            currentMediaTime > bufferStartMediaTime + 0.05) {
          setState("playing", "Live stream playing.", false);
          return;
        }

        scheduleRecovery(false);
      }, bufferGraceMs);
    }

    function sampleProgress() {
      var mediaTime;
      var sampleTime;

      if (!wantsPlayback || audio.paused || internalReload) {
        lastMediaTime = null;
        noProgressSince = null;
        return;
      }

      mediaTime = Number(audio.currentTime);
      if (!Number.isFinite(mediaTime)) {
        lastMediaTime = null;
        noProgressSince = null;
        return;
      }

      sampleTime = now();
      if (lastMediaTime === null || mediaTime > lastMediaTime + 0.05) {
        if (lastMediaTime !== null) {
          hasObservedProgress = true;
          lastProgressAt = sampleTime;
          clearTimer("buffer");
          if (retryTimerId !== null) {
            clearTimer("retry");
            setState("playing", "Live stream playing.", false);
          }
        }
        noProgressSince = null;
      } else if (hasObservedProgress && noProgressSince === null) {
        noProgressSince = sampleTime;
      } else if (hasObservedProgress && sampleTime - noProgressSince >= progressStallMs) {
        noProgressSince = null;
        scheduleRecovery(false);
      }

      lastMediaTime = mediaTime;
    }

    function startProgressWatchdog() {
      resetProgressWatchdog();
      lastMediaTime = Number.isFinite(Number(audio.currentTime)) ? Number(audio.currentTime) : null;
      progressTimerId = clock.setInterval(sampleProgress, progressIntervalMs);
    }

    function handlePlay() {
      var wasInternalReload = internalReload;

      wantsPlayback = true;
      internalReload = false;
      clearTimer("retry");
      clearTimer("buffer");

      if (suspended) {
        return;
      }

      interactionRequired = false;
      if (isOffline()) {
        clearTimer("connect");
        showOffline();
        return;
      }

      if (!wasInternalReload) {
        retryIndex = 0;
        generation += 1;
        startConnectTimeout(generation);
        setState("connecting", "Connecting to the live stream…", false);
      }
    }

    function handlePlaying() {
      if (!wantsPlayback || suspended) {
        return;
      }

      if (isOffline()) {
        showOffline();
        return;
      }

      interactionRequired = false;
      internalReload = false;
      clearTimer("retry");
      clearTimer("buffer");
      clearTimer("connect");
      clearTimer("stable");
      setState("playing", "Live stream playing.", false);
      startProgressWatchdog();

      stableTimerId = clock.setTimeout(function () {
        stableTimerId = null;
        if (hasObservedProgress && lastProgressAt !== null &&
            now() - lastProgressAt <= progressIntervalMs * 2) {
          retryIndex = 0;
        }
      }, stablePlaybackMs);
    }

    function handlePause() {
      if (wantsPlayback && audio.ended) {
        return;
      }

      stopPlaybackIntent();
    }

    function handleCanPlay() {
      clearTimer("buffer");
    }

    function handleError() {
      var mediaErrorCode = audio.error && audio.error.code;

      if (!wantsPlayback) {
        return;
      }

      if (internalReload && mediaErrorCode === 1) {
        return;
      }

      scheduleRecovery(false);
    }

    function handleEnded() {
      if (wantsPlayback) {
        scheduleRecovery(false);
      }
    }

    function handleOffline() {
      if (!wantsPlayback) {
        return;
      }

      generation += 1;
      internalReload = false;
      clearRecoveryTimers();
      showOffline();
    }

    function handleOnline() {
      if (!wantsPlayback) {
        return;
      }

      if (interactionRequired) {
        showInteractionRequired();
        return;
      }

      scheduleRecovery(true, "Internet connection restored. Reconnecting…");
    }

    function handleRetry() {
      wantsPlayback = true;
      interactionRequired = false;
      suspended = false;
      retryIndex = 0;
      generation += 1;
      clearRecoveryTimers();
      attemptRecovery(true);
    }

    function handlePageHide() {
      if (!wantsPlayback) {
        return;
      }

      suspended = true;
      generation += 1;
      internalReload = false;
      clearRecoveryTimers();
    }

    function handlePageShow(event) {
      if (!suspended || !wantsPlayback || (event && event.persisted === false)) {
        suspended = false;
        return;
      }

      suspended = false;
      scheduleRecovery(true);
    }

    function destroy() {
      generation += 1;
      clearRecoveryTimers();
      listeners.forEach(function (listener) {
        listener.target.removeEventListener(listener.eventName, listener.handler);
      });
      listeners = [];
      rootElement.removeAttribute("data-player-initialised");
    }

    rootElement.setAttribute("data-player-initialised", "true");
    setState("idle", "Press play to listen live.", false);

    addListener(audio, "play", handlePlay);
    addListener(audio, "playing", handlePlaying);
    addListener(audio, "pause", handlePause);
    addListener(audio, "canplay", handleCanPlay);
    addListener(audio, "waiting", startBufferGrace);
    addListener(audio, "stalled", startBufferGrace);
    addListener(audio, "error", handleError);
    addListener(audio, "ended", handleEnded);
    addListener(retryButton, "click", handleRetry);
    addListener(windowObject, "offline", handleOffline);
    addListener(windowObject, "online", handleOnline);
    addListener(windowObject, "pagehide", handlePageHide);
    addListener(windowObject, "pageshow", handlePageShow);

    return {
      destroy: destroy
    };
  }

  function init() {
    var controllers = [];
    var roots;

    if (!windowObject.document || !windowObject.document.querySelectorAll) {
      return controllers;
    }

    roots = windowObject.document.querySelectorAll("[data-listen-live-player]");
    for (var index = 0; index < roots.length; index += 1) {
      if (roots[index].getAttribute("data-player-initialised") === "true") {
        continue;
      }

      var controller = createController(roots[index]);
      if (controller) {
        controllers.push(controller);
      }
    }

    return controllers;
  }

  return {
    createController: createController,
    init: init
  };
}));
