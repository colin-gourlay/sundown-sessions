(function () {
  "use strict";

  var chordTimeout;
  var awaitingGoTo = false;
  var shortcutTimeoutMs = 1500;
  var routes = {
    h: { label: "Home", path: "/" },
    s: { label: "Shows", path: "/shows/" },
    a: { label: "About", path: "/about/" },
    c: { label: "Contact", path: "/contact/" },
  };

  function isEditableTarget(target) {
    if (!target) return false;
    var editableSelector = "input, textarea, select, [contenteditable=''], [contenteditable='true'], [role='textbox']";
    return Boolean(target.closest && target.closest(editableSelector));
  }

  function resetChord() {
    awaitingGoTo = false;
    window.clearTimeout(chordTimeout);
  }

  function getHomeUrl() {
    var homeLink = document.querySelector('a[title*="home" i][href], a[href="/"]');
    return homeLink ? homeLink.href : window.location.origin + "/";
  }

  function withSiteRoot(path) {
    return new URL(path.replace(/^\//, ""), getHomeUrl()).href;
  }

  function goTo(path) {
    window.location.assign(withSiteRoot(path));
  }

  function focusSearch() {
    var activeSearchInput = document.querySelector("#search input[type='search'], #search input, input[type='search'], #search-query");
    if (activeSearchInput) {
      activeSearchInput.focus();
      if (typeof activeSearchInput.select === "function") activeSearchInput.select();
      return;
    }

    var searchButton = document.getElementById("search-button") || document.getElementById("search-button-mobile");
    if (searchButton) {
      searchButton.click();
      window.setTimeout(function () {
        var openedSearchInput = document.querySelector("#search input[type='search'], #search input, input[type='search'], #search-query");
        if (openedSearchInput) openedSearchInput.focus();
      }, 50);
      return;
    }

    goTo("/search/");
  }

  function toggleTheme() {
    var switcher = document.getElementById("appearance-switcher") || document.getElementById("appearance-switcher-mobile");
    if (switcher) switcher.click();
  }

  function ensureHelpDialog() {
    var existing = document.getElementById("keyboard-shortcuts-help");
    if (existing) return existing;

    var dialog = document.createElement("section");
    dialog.id = "keyboard-shortcuts-help";
    dialog.className = "keyboard-shortcuts-help";
    dialog.setAttribute("role", "dialog");
    dialog.setAttribute("aria-modal", "true");
    dialog.setAttribute("aria-labelledby", "keyboard-shortcuts-help-title");
    dialog.setAttribute("hidden", "");
    dialog.innerHTML = [
      '<div class="keyboard-shortcuts-help__panel" role="document">',
      '<button type="button" class="keyboard-shortcuts-help__close" aria-label="Close keyboard shortcuts help">&times;</button>',
      '<h2 id="keyboard-shortcuts-help-title">Keyboard shortcuts</h2>',
      '<p>Use these shortcuts anywhere on the site. They are ignored while typing in form fields.</p>',
      '<dl>',
      '<div><dt><kbd>g</kbd> then <kbd>h</kbd></dt><dd>Go to Home</dd></div>',
      '<div><dt><kbd>g</kbd> then <kbd>s</kbd></dt><dd>Go to Shows</dd></div>',
      '<div><dt><kbd>g</kbd> then <kbd>a</kbd></dt><dd>Go to About</dd></div>',
      '<div><dt><kbd>g</kbd> then <kbd>c</kbd></dt><dd>Go to Contact</dd></div>',
      '<div><dt><kbd>/</kbd></dt><dd>Open or focus Search</dd></div>',
      '<div><dt><kbd>g</kbd> then <kbd>d</kbd></dt><dd>Toggle dark mode</dd></div>',
      '<div><dt><kbd>?</kbd></dt><dd>Show this help</dd></div>',
      '</dl>',
      '</div>',
    ].join("");

    document.body.appendChild(dialog);
    dialog.querySelector(".keyboard-shortcuts-help__close").addEventListener("click", closeHelp);
    dialog.addEventListener("click", function (event) {
      if (event.target === dialog) closeHelp();
    });
    return dialog;
  }

  function openHelp() {
    var dialog = ensureHelpDialog();
    dialog.removeAttribute("hidden");
    var closeButton = dialog.querySelector("button");
    if (closeButton) closeButton.focus();
  }

  function closeHelp() {
    var dialog = document.getElementById("keyboard-shortcuts-help");
    if (dialog) dialog.setAttribute("hidden", "");
  }

  function addShortcutTitle(selector, shortcut) {
    document.querySelectorAll(selector).forEach(function (element) {
      var title = element.getAttribute("title") || element.getAttribute("aria-label") || element.textContent.trim();
      if (!title || title.indexOf(shortcut) !== -1) return;
      element.setAttribute("title", title + " (" + shortcut + ")");
    });
  }

  function annotateShortcuts() {
    addShortcutTitle('a[href="/"], a[href$="/"][title*="home" i]', "g then h");
    addShortcutTitle('a[href="/shows/"], a[href$="/shows/"]', "g then s");
    addShortcutTitle('a[href="/about/"], a[href$="/about/"]', "g then a");
    addShortcutTitle('a[href="/contact/"], a[href$="/contact/"]', "g then c");
    addShortcutTitle('#search-button, #search-button-mobile, a[href="/search/"], a[href$="/search/"]', "/");
    addShortcutTitle('#appearance-switcher, #appearance-switcher-mobile', "g then d");
  }

  document.addEventListener("keydown", function (event) {
    if (event.defaultPrevented || event.ctrlKey || event.metaKey || event.altKey || isEditableTarget(event.target)) return;

    var key = event.key.toLowerCase();
    if (key === "escape") {
      closeHelp();
      resetChord();
      return;
    }

    if (key === "?") {
      event.preventDefault();
      resetChord();
      openHelp();
      return;
    }

    if (key === "/") {
      event.preventDefault();
      resetChord();
      focusSearch();
      return;
    }

    if (awaitingGoTo) {
      if (routes[key]) {
        event.preventDefault();
        goTo(routes[key].path);
      } else if (key === "d") {
        event.preventDefault();
        toggleTheme();
      }
      resetChord();
      return;
    }

    if (key === "g") {
      awaitingGoTo = true;
      window.clearTimeout(chordTimeout);
      chordTimeout = window.setTimeout(resetChord, shortcutTimeoutMs);
    }
  });

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", annotateShortcuts);
  } else {
    annotateShortcuts();
  }
})();
