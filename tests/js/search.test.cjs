const assert = require("node:assert/strict");
const test = require("node:test");

const createSearch = require("../../src/assets/js/search.js");
const Fuse = require("../../src/themes/blowfish/assets/lib/fuse/fuse.min.cjs");

class FakeElement {
  constructor(documentObject, tagName = "DIV") {
    this.attributes = new Map();
    this.documentObject = documentObject;
    this.firstChild = null;
    this.lastChild = null;
    this.listeners = new Map();
    this.parentElement = null;
    this.style = {};
    this.tagName = tagName;
    this.textContent = "";
    this.value = "";
    this._innerHTML = "";
  }

  addEventListener(type, listener) {
    this.listeners.set(type, listener);
  }

  dispatch(type) {
    this.listeners.get(type).call(this, { currentTarget: this });
  }

  focus() {
    this.documentObject.activeElement = this;
  }

  getAttribute(name) {
    return this.attributes.has(name) ? this.attributes.get(name) : null;
  }

  setAttribute(name, value) {
    this.attributes.set(name, String(value));
  }

  set innerHTML(value) {
    this._innerHTML = value;
    this.textContent = value.replace(/<[^>]+>/g, "");
    this.firstChild = null;
    this.lastChild = null;

    if (value.includes('<li class="mb-2">')) {
      const item = new FakeElement(this.documentObject, "LI");
      const link = new FakeElement(this.documentObject, "A");
      item.firstElementChild = link;
      link.parentElement = item;
      this.firstChild = item;
      this.lastChild = item;
    }
  }

  get innerHTML() {
    return this._innerHTML;
  }
}

function keyboardEvent(key) {
  return {
    key,
    prevented: false,
    preventDefault() {
      this.prevented = true;
    }
  };
}

function createHarness({ FuseImplementation, searchData = [] } = {}) {
  const elements = new Map();
  const listeners = new Map();
  const documentObject = {
    activeElement: null,
    body: null,
    addEventListener(type, listener) {
      listeners.set(type, listener);
    },
    createElement(tagName) {
      return new FakeElement(documentObject, tagName.toUpperCase());
    },
    getElementById(id) {
      return elements.get(id) || null;
    }
  };
  documentObject.body = new FakeElement(documentObject, "BODY");
  documentObject.activeElement = documentObject.body;

  ["search-button", "search-button-mobile", "close-search-button", "search-wrapper", "search-modal", "search-query", "search-results"].forEach((id) => {
    elements.set(id, new FakeElement(documentObject, id === "search-query" ? "INPUT" : "DIV"));
  });
  elements.get("search-wrapper").setAttribute("data-url", "/");

  const searches = [];
  class FakeFuse {
    search(query) {
      searches.push(query);
      if (query === "match") {
        return [{ item: { title: "Matching show", permalink: "/shows/match/", section: "Shows", summary: "A result" } }];
      }
      return [];
    }
  }

  class FakeXMLHttpRequest {
    open() {}

    send() {
      this.readyState = 4;
      this.status = 200;
      this.responseText = JSON.stringify(searchData);
      this.onreadystatechange();
    }
  }

  let modalOpen = false;
  const controller = createSearch({
    document: documentObject,
    Fuse: FuseImplementation || FakeFuse,
    XMLHttpRequest: FakeXMLHttpRequest,
    sundownModalFocus: {
      createController() {
        return {
          close() {
            modalOpen = false;
          },
          isTop() {
            return modalOpen;
          },
          open(invoker, initialFocus) {
            modalOpen = true;
            initialFocus.focus();
          },
          trapTab() {}
        };
      }
    }
  });

  return {
    controller,
    input: elements.get("search-query"),
    keydown(event) {
      listeners.get("keydown")(event);
    },
    output: elements.get("search-results"),
    searches
  };
}

test("empty and whitespace-only queries do not search the index", () => {
  const harness = createHarness();

  harness.controller.open();
  harness.controller.executeQuery("");
  harness.controller.executeQuery("  \n\t  ");

  assert.deepEqual(harness.searches, []);
  assert.equal(harness.output.innerHTML, "");
});

test("clearing a query removes results from keyboard navigation", () => {
  const harness = createHarness();
  harness.controller.open();
  harness.controller.executeQuery("match");

  const resultNavigation = keyboardEvent("ArrowDown");
  harness.keydown(resultNavigation);
  assert.equal(resultNavigation.prevented, true);
  assert.notEqual(harness.input.documentObject.activeElement, harness.input);

  harness.input.value = "   ";
  harness.input.dispatch("input");
  const emptyNavigation = keyboardEvent("ArrowDown");
  harness.keydown(emptyNavigation);

  assert.equal(emptyNavigation.prevented, false);
  assert.equal(harness.output.innerHTML, "");
  assert.deepEqual(harness.searches, ["match"]);
});

test("a no-result query shows restrained discovery links", () => {
  const harness = createHarness();

  harness.controller.executeQuery("  Redemption ZERO  ");

  assert.deepEqual(harness.searches, ["Redemption ZERO"]);
  assert.match(harness.output.innerHTML, /No search results found\./);
  assert.match(harness.output.innerHTML, /href="\/shows\/">Shows<\/a>/);
  assert.match(harness.output.innerHTML, /href="\/artists\/">Artists<\/a>/);
  assert.match(harness.output.innerHTML, /href="\/releases\/">Releases<\/a>/);
  assert.match(harness.output.innerHTML, /href="\/tracks\/">Tracks<\/a>/);
});

test("space and hyphen separators match canonical taxonomy titles symmetrically", () => {
  const harness = createHarness({
    FuseImplementation: Fuse,
    searchData: [
      { title: "Post-Punk", permalink: "/genres/post-punk/", section: "Genres", summary: "" },
      { title: "New Wave", permalink: "/genres/new-wave/", section: "Genres", summary: "" }
    ]
  });
  harness.controller.open();

  harness.controller.executeQuery("Post Punk");
  assert.match(harness.output.innerHTML, />Post-Punk<\/div>/);

  harness.controller.executeQuery("Post-Punk");
  assert.match(harness.output.innerHTML, />Post-Punk<\/div>/);

  harness.controller.executeQuery("New-Wave");
  assert.match(harness.output.innerHTML, />New Wave<\/div>/);
});

test("exact artist, release and track titles retain their relevance ordering", () => {
  const exactItems = [
    { title: "Magazine", permalink: "/artists/magazine/", section: "Artists", summary: "" },
    { title: "Real Life", permalink: "/releases/real-life/", section: "Releases", summary: "" },
    { title: "Shot by Both Sides", permalink: "/tracks/shot-by-both-sides/", section: "Tracks", summary: "" }
  ];
  const searchData = exactItems.concat(exactItems.map((item) => ({
    title: "Related page",
    permalink: item.permalink + "related/",
    section: item.section,
    summary: item.title
  })));
  const harness = createHarness({ FuseImplementation: Fuse, searchData });
  harness.controller.open();

  exactItems.forEach((item) => {
    harness.controller.executeQuery(item.title);
    assert.ok(
      harness.output.innerHTML.indexOf(`>${item.title}</div>`) < harness.output.innerHTML.indexOf(">Related page</div>"),
      `${item.section} exact-title result should rank before a summary match`
    );
  });
});
