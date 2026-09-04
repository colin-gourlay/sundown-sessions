var fuse;
var showButton = document.getElementById("search-button");
var showButtonMobile = document.getElementById("search-button-mobile");
var hideButton = document.getElementById("close-search-button");
var wrapper = document.getElementById("search-wrapper");
var modal = document.getElementById("search-modal");
var input = document.getElementById("search-query");
var output = document.getElementById("search-results");
var first = output.firstChild;
var last = output.lastChild;
var searchVisible = false;
var indexed = false;
var hasResults = false;
var previousBodyOverflow = "";
var modalFocus = window.sundownModalFocus.createController(modal);

showButton ? showButton.addEventListener("click", displaySearch) : null;
showButtonMobile ? showButtonMobile.addEventListener("click", displaySearch) : null;
hideButton.addEventListener("click", hideSearch);
wrapper.addEventListener("click", hideSearch);
modal.addEventListener("click", function (event) {
  event.stopPropagation();
  event.stopImmediatePropagation();
  return false;
});
document.addEventListener("keydown", function (event) {
  if (event.key === "Tab") modalFocus.trapTab(event);

  if (event.key === "/") {
    var active = document.activeElement;
    var tag = active.tagName;
    var isInputField = tag === "INPUT" || tag === "TEXTAREA" || active.isContentEditable;

    if (!searchVisible && !isInputField) {
      event.preventDefault();
      displaySearch(event);
    }
  }

  if (event.key === "Escape" && modalFocus.isTop()) {
    event.preventDefault();
    hideSearch();
  }

  if (event.key === "ArrowDown" && searchVisible && hasResults) {
    event.preventDefault();
    if (document.activeElement === input) first.focus();
    else if (document.activeElement === last) last.focus();
    else document.activeElement.parentElement.nextSibling.firstElementChild.focus();
  }

  if (event.key === "ArrowUp" && searchVisible && hasResults) {
    event.preventDefault();
    if (document.activeElement === input || document.activeElement === first) input.focus();
    else document.activeElement.parentElement.previousSibling.firstElementChild.focus();
  }

  if (event.key === "Enter" && searchVisible && hasResults) {
    event.preventDefault();
    if (document.activeElement === input) first.focus();
    else document.activeElement.click();
  }
});

input.onkeyup = function () {
  executeQuery(this.value);
};

function displaySearch(event) {
  if (!indexed) buildIndex();
  if (searchVisible) return;

  previousBodyOverflow = document.body.style.overflow;
  document.body.style.overflow = "hidden";
  wrapper.style.visibility = "visible";
  searchVisible = true;
  modalFocus.open(event && event.currentTarget, input);
}

function hideSearch() {
  if (!searchVisible) return;

  document.body.style.overflow = previousBodyOverflow;
  wrapper.style.visibility = "hidden";
  input.value = "";
  output.innerHTML = "";
  hasResults = false;
  searchVisible = false;
  modalFocus.close();
  wrapper.setAttribute("aria-hidden", "true");
}

function fetchJSON(path, callback) {
  var httpRequest = new XMLHttpRequest();
  httpRequest.onreadystatechange = function () {
    if (httpRequest.readyState === 4 && httpRequest.status === 200) {
      var data = JSON.parse(httpRequest.responseText);
      if (callback) callback(data);
    }
  };
  httpRequest.open("GET", path);
  httpRequest.send();
}

function buildIndex() {
  var baseURL = wrapper.getAttribute("data-url").replace(/\/?$/, "/");
  fetchJSON(baseURL + "index.json", function (data) {
    fuse = new Fuse(data, {
      shouldSort: true,
      ignoreLocation: true,
      threshold: 0.0,
      includeMatches: true,
      keys: [
        { name: "title", weight: 0.8 },
        { name: "section", weight: 0.2 },
        { name: "summary", weight: 0.6 },
        { name: "content", weight: 0.4 }
      ]
    });
    indexed = true;
  });
}

function executeQuery(term) {
  if (!indexed) buildIndex();
  if (!fuse) return;

  var results = fuse.search(term);
  var resultsHTML = "";
  results.forEach(function (value) {
    var div = document.createElement("div");
    div.innerHTML = value.item.summary;
    value.item.summary = div.textContent || div.innerText || "";
    var title = value.item.externalUrl
      ? value.item.title + '<span class="text-xs ml-2 align-center cursor-default text-neutral-400 dark:text-neutral-500">' + value.item.externalUrl + "</span>"
      : value.item.title;
    var linkconfig = value.item.externalUrl
      ? 'target="_blank" rel="noopener" href="' + value.item.externalUrl + '"'
      : 'href="' + value.item.permalink + '"';
    resultsHTML += `<li class="mb-2">
      <a class="flex items-center px-3 py-2 rounded-md appearance-none bg-neutral-100 dark:bg-neutral-700 focus:bg-primary-100 hover:bg-primary-100 dark:hover:bg-primary-900 dark:focus:bg-primary-900 focus:outline-dotted focus:outline-transparent focus:outline-2"
      ${linkconfig} tabindex="0">
        <div class="grow">
          <div class="-mb-1 text-lg font-bold">${title}</div>
          <div class="text-sm text-neutral-500 dark:text-neutral-400">${value.item.section}<span class="px-2 text-primary-500">&middot;</span>${value.item.date ? value.item.date : ""}</span></div>
          <div class="text-sm italic">${value.item.summary}</div>
        </div>
        <div class="ml-2 ltr:block rtl:hidden text-neutral-500">&rarr;</div>
        <div class="mr-2 ltr:hidden rtl:block text-neutral-500">&larr;</div>
      </a>
    </li>`;
  });

  hasResults = results.length > 0;
  output.innerHTML = resultsHTML;
  if (hasResults && output.firstChild) {
    first = output.firstChild.firstElementChild;
    last = output.lastChild.firstElementChild;
  }
}
