(function () {
  'use strict';

  var SECTION_LABELS = {
    artists:  'Artist',
    tracks:   'Track',
    releases: 'Album',
    shows:    'Show'
  };

  var searchInput  = document.getElementById('search-input');
  var resultsEl    = document.getElementById('search-results');
  var fuseInstance = null;

  function buildSubtitle(item) {
    switch (item.section) {
      case 'artists':
        return item.tags && item.tags.length ? item.tags.slice(0, 3).join(' · ') : '';
      case 'tracks':
        return [item.artist, item.album].filter(Boolean).join(' — ');
      case 'releases':
        return item.artist || '';
      case 'shows':
        return item.description || '';
      default:
        return '';
    }
  }

  function escapeHTML(str) {
    return String(str)
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;');
  }

  function renderResults(results) {
    if (!results.length) {
      resultsEl.innerHTML = '<p class="f5 mid-gray">No results found.</p>';
      return;
    }

    var html = '<ul class="list pa0 ma0">';
    results.forEach(function (r) {
      var item     = r.item;
      var label    = SECTION_LABELS[item.section] || item.section;
      var subtitle = buildSubtitle(item);

      html += '<li class="pv3 bb b--black-10">';
      html += '<a href="' + item.url + '" class="no-underline dark-gray dim">';
      html += '<span class="db f4 fw5">' + escapeHTML(item.title) + '</span>';
      if (subtitle) {
        html += '<span class="db f6 gray mt1">' + escapeHTML(subtitle) + '</span>';
      }
      html += '</a>';
      html += '<span class="dib mt1 f7 fw6 ttu tracked br2 ph2 pv1 bg-light-gray mid-gray">' + escapeHTML(label) + '</span>';
      html += '</li>';
    });
    html += '</ul>';

    resultsEl.innerHTML = html;
  }

  function onInput() {
    var query = searchInput.value.trim();
    if (!query) {
      resultsEl.innerHTML = '';
      return;
    }
    if (!fuseInstance) { return; }
    var results = fuseInstance.search(query, { limit: 20 });
    renderResults(results);
  }

  var indexUrl = searchInput.dataset.indexUrl;

  if (!indexUrl) {
    console.error('Search: data-index-url attribute is missing from the search input element.');
    resultsEl.innerHTML = '<p class="f5 dark-red">Search is temporarily unavailable. Please try again later.</p>';
    return;
  }

  fetch(indexUrl)
    .then(function (r) { return r.json(); })
    .then(function (data) {
      fuseInstance = new Fuse(data, {
        keys: [
          { name: 'title',       weight: 0.4 },
          { name: 'artist',      weight: 0.25 },
          { name: 'album',       weight: 0.15 },
          { name: 'keywords',    weight: 0.1 },
          { name: 'tags',        weight: 0.05 },
          { name: 'description', weight: 0.05 }
        ],
        threshold:          0.25,
        includeScore:       true,
        ignoreLocation:     true,
        minMatchCharLength: 2
      });
      if (searchInput.value.trim()) { onInput(); }
    })
    .catch(function (err) {
      console.error('Search index could not be loaded:', err);
      resultsEl.innerHTML = '<p class="f5 dark-red">Search is temporarily unavailable. Please try again later.</p>';
    });

  searchInput.addEventListener('input', onInput);
  searchInput.focus();
}());
