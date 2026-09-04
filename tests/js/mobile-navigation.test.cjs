const assert = require("node:assert/strict");
const test = require("node:test");

const createMobileNavigation = require("../../src/assets/js/mobile-navigation.js");

class FakeElement {
  constructor(documentObject) {
    this.attributes = new Map();
    this.documentObject = documentObject;
    this.listeners = new Map();
    this.queryResults = new Map();
  }

  addEventListener(type, listener) {
    this.listeners.set(type, listener);
  }

  click() {
    this.listeners.get("click")({ currentTarget: this });
  }

  focus() {
    this.documentObject.activeElement = this;
  }

  getAttribute(name) {
    return this.attributes.has(name) ? this.attributes.get(name) : null;
  }

  hasAttribute(name) {
    return this.attributes.has(name);
  }

  querySelector(selector) {
    return this.queryResults.get(selector) || null;
  }

  querySelectorAll(selector) {
    return this.queryResults.get(selector) || [];
  }

  removeAttribute(name) {
    this.attributes.delete(name);
  }

  setAttribute(name, value) {
    this.attributes.set(name, String(value));
  }
}

function createHarness() {
  const elements = new Map();
  const listeners = new Map();
  const documentObject = {
    activeElement: null,
    addEventListener(type, listener) {
      listeners.set(type, listener);
    },
    getElementById(id) {
      return elements.get(id) || null;
    }
  };
  const openButton = new FakeElement(documentObject);
  const dialog = new FakeElement(documentObject);
  const closeButton = new FakeElement(documentObject);
  const submenuButton = new FakeElement(documentObject);
  const submenu = new FakeElement(documentObject);
  let modalOpen = false;
  let trappedEvent = null;
  const modalFocus = {
    createController() {
      return {
        close() {
          modalOpen = false;
          openButton.focus();
        },
        isOpen() {
          return modalOpen;
        },
        isTop() {
          return modalOpen;
        },
        open(invoker, initialFocus) {
          assert.equal(invoker, openButton);
          modalOpen = true;
          initialFocus.focus();
        },
        trapTab(event) {
          trappedEvent = event;
        }
      };
    }
  };

  elements.set("mobile-menu-open", openButton);
  elements.set("mobile-navigation", dialog);
  elements.set("shows-submenu", submenu);
  dialog.queryResults.set("[data-mobile-menu-close]", closeButton);
  dialog.queryResults.set("[data-mobile-submenu-toggle]", [submenuButton]);
  submenuButton.setAttribute("aria-controls", "shows-submenu");
  submenuButton.setAttribute("aria-expanded", "true");

  const controller = createMobileNavigation({
    document: documentObject,
    sundownModalFocus: modalFocus
  });

  return {
    closeButton,
    controller,
    dialog,
    documentObject,
    keydown(event) {
      listeners.get("keydown")(event);
    },
    openButton,
    submenu,
    submenuButton,
    trappedEvent() {
      return trappedEvent;
    }
  };
}

test("the menu buttons expose state and restore focus when closing", () => {
  const harness = createHarness();

  harness.openButton.click();
  assert.equal(harness.openButton.getAttribute("aria-expanded"), "true");
  assert.equal(harness.dialog.getAttribute("aria-hidden"), "false");
  assert.equal(harness.dialog.getAttribute("aria-modal"), "true");
  assert.equal(harness.dialog.hasAttribute("inert"), false);
  assert.equal(harness.documentObject.activeElement, harness.closeButton);

  harness.closeButton.click();
  assert.equal(harness.openButton.getAttribute("aria-expanded"), "false");
  assert.equal(harness.dialog.getAttribute("aria-hidden"), "true");
  assert.equal(harness.dialog.getAttribute("aria-modal"), null);
  assert.equal(harness.dialog.hasAttribute("inert"), true);
  assert.equal(harness.documentObject.activeElement, harness.openButton);
});

test("Escape closes the topmost menu and Tab is passed to the focus trap", () => {
  const harness = createHarness();
  const tabEvent = { key: "Tab" };
  harness.openButton.click();

  harness.keydown(tabEvent);
  assert.equal(harness.trappedEvent(), tabEvent);

  const escapeEvent = {
    key: "Escape",
    prevented: false,
    preventDefault() {
      this.prevented = true;
    }
  };
  harness.keydown(escapeEvent);
  assert.equal(escapeEvent.prevented, true);
  assert.equal(harness.openButton.getAttribute("aria-expanded"), "false");
});

test("submenu buttons update accessible state and remove collapsed links from interaction", () => {
  const harness = createHarness();

  harness.submenuButton.click();
  assert.equal(harness.submenuButton.getAttribute("aria-expanded"), "false");
  assert.equal(harness.submenu.getAttribute("data-open"), "false");
  assert.equal(harness.submenu.getAttribute("aria-hidden"), "true");
  assert.equal(harness.submenu.hasAttribute("inert"), true);

  harness.submenuButton.click();
  assert.equal(harness.submenuButton.getAttribute("aria-expanded"), "true");
  assert.equal(harness.submenu.getAttribute("aria-hidden"), "false");
  assert.equal(harness.submenu.hasAttribute("inert"), false);
});
