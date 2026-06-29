(function () {
  "use strict";

  var REFRESH_INTERVAL_MS = 60000;
  var UK_TIME_ZONE = "Europe/London";
  var BROADCAST_DAY = "Tue";
  var BROADCAST_START_MINUTES = 20 * 60;
  var BROADCAST_END_MINUTES = 22 * 60;

  var status = document.querySelector("[data-live-status]");
  var standfirst = document.querySelector("[data-live-standfirst]");
  var playerBadge = document.querySelector("[data-live-player-badge]");
  var playerBadgeLabel = document.querySelector("[data-live-player-badge-label]");
  var playerBadgeNote = document.querySelector("[data-live-player-badge-note]");

  if (!status || !standfirst || !window.Intl || !window.Intl.DateTimeFormat) {
    return;
  }

  // Keep this tied to UK civil time so daylight saving changes are handled by the browser.
  var formatter = new Intl.DateTimeFormat("en-GB", {
    timeZone: UK_TIME_ZONE,
    weekday: "short",
    hour: "2-digit",
    minute: "2-digit",
    hourCycle: "h23"
  });

  function getUkTimeParts(date) {
    var parts = formatter.formatToParts(date || new Date());
    var values = {};

    parts.forEach(function (part) {
      if (part.type !== "literal") {
        values[part.type] = part.value;
      }
    });

    var hour = parseInt(values.hour, 10);
    var minute = parseInt(values.minute, 10);

    if (Number.isNaN(hour) || Number.isNaN(minute)) {
      return {
        weekday: "",
        minutes: -1
      };
    }

    return {
      weekday: values.weekday,
      minutes: (hour * 60) + minute
    };
  }

  function isLiveNow(date) {
    var ukTime = getUkTimeParts(date);

    return ukTime.weekday === BROADCAST_DAY &&
      ukTime.minutes >= BROADCAST_START_MINUTES &&
      ukTime.minutes < BROADCAST_END_MINUTES;
  }

  function setPlayerBadge(live) {
    if (!playerBadge || !playerBadgeLabel || !playerBadgeNote) {
      return;
    }

    playerBadge.setAttribute("data-broadcast-state", live ? "live" : "off-air");
    playerBadge.setAttribute("aria-label", live ? "Live stream status: live now" : "Live stream status: station stream available");
    playerBadgeLabel.textContent = live ? "LIVE NOW" : "Station Stream";
    playerBadgeNote.textContent = live ? "Broadcasting live until 10pm" : "Live Tuesdays, 8pm\u201310pm UK time";
  }

  function render() {
    var live = isLiveNow();
    var archiveUrl = standfirst.getAttribute("data-archive-url") || "/shows/";

    status.setAttribute("data-broadcast-state", live ? "live" : "off-air");

    if (live) {
      status.innerHTML = "<span class=\"listen-live-broadcast-cue__dot\" aria-hidden=\"true\"></span>On Air Now &mdash; Broadcasting live until 10pm.";
      standfirst.textContent = "No two broadcasts are ever quite the same. Join Sundown Sessions live for carefully curated alternative music, exclusive first plays, artist interviews and unexpected discoveries. Press play, settle in and discover what's drifting through Sundown Sessions tonight.";
    } else {
      status.textContent = "Next live broadcast: Tuesday, 8pm\u201310pm (UK time).";
      standfirst.innerHTML = "Sundown Sessions broadcasts live every Tuesday from 8pm to 10pm (UK time). Join us for carefully curated alternative music, exclusive first plays, artist interviews and unexpected discoveries. Between broadcasts, explore the <a href=\"" + archiveUrl + "\">archive</a> or enjoy the station stream.";
    }

    setPlayerBadge(live);
  }

  render();
  window.setInterval(render, REFRESH_INTERVAL_MS);
}());
