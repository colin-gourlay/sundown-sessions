const assert = require("node:assert/strict");
const test = require("node:test");

const createModalFocus = require("../../src/assets/js/modal-focus.js");

class FakeElement {
  constructor(documentObject) {
    this.attributes = new Map();
    this.documentObject = documentObject;
    this.children = [];
    this.focusables = [];
    this.hidden = false;
    this.isConnected = true;
    this.parentElement = null;
  }

  append(child) {
    child.parentElement = this;
    this.children.push(child);
  }

  contains(element) {
    var candidate = element;
    while (candidate) {
      if (candidate === this) return true;
      candidate = candidate.parentElement;
    }
    return false;
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

  querySelectorAll() {
    return this.focusables;
  }

  removeAttribute(name) {
    this.attributes.delete(name);
  }

  setAttribute(name, value) {
    this.attributes.set(name, String(value));
  }
}

function createHarness() {
  const documentObject = { activeElement: null, body: null };
  const body = new FakeElement(documentObject);
  documentObject.body = body;

  const page = new FakeElement(documentObject);
  const wrapper = new FakeElement(documentObject);
  const modal = new FakeElement(documentObject);
  const input = new FakeElement(documentObject);
  const close = new FakeElement(documentObject);
  const invoker = new FakeElement(documentObject);

  body.append(page);
  page.append(invoker);
  page.append(wrapper);
  wrapper.append(modal);
  modal.append(input);
  modal.append(close);
  modal.focusables = [input, close];
  wrapper.setAttribute("aria-hidden", "true");

  return {
    close,
    controller: createModalFocus({ document: documentObject }).createController(modal),
    documentObject,
    input,
    invoker,
    modal,
    page,
    wrapper
  };
}

function tabEvent(shiftKey = false) {
  return {
    key: "Tab",
    shiftKey,
    prevented: false,
    preventDefault() {
      this.prevented = true;
    }
  };
}

test("opening a modal focuses its initial control and makes the background inert", () => {
  const harness = createHarness();

  harness.controller.open(harness.invoker, harness.input);

  assert.equal(harness.documentObject.activeElement, harness.input);
  assert.equal(harness.page.hasAttribute("inert"), false);
  assert.equal(harness.invoker.hasAttribute("inert"), true);
  assert.equal(harness.invoker.getAttribute("aria-hidden"), "true");
  assert.equal(harness.wrapper.hasAttribute("inert"), false);
  assert.equal(harness.wrapper.hasAttribute("aria-hidden"), false);
});

test("Tab and Shift+Tab wrap focus within the modal", () => {
  const harness = createHarness();
  harness.controller.open(harness.invoker, harness.input);

  harness.close.focus();
  const forwards = tabEvent();
  harness.controller.trapTab(forwards);
  assert.equal(forwards.prevented, true);
  assert.equal(harness.documentObject.activeElement, harness.input);

  const backwards = tabEvent(true);
  harness.controller.trapTab(backwards);
  assert.equal(backwards.prevented, true);
  assert.equal(harness.documentObject.activeElement, harness.close);
});

test("closing restores background state and focus to the invoking control", () => {
  const harness = createHarness();
  harness.page.setAttribute("aria-hidden", "false");
  harness.controller.open(harness.invoker, harness.input);

  harness.controller.close();

  assert.equal(harness.page.hasAttribute("inert"), false);
  assert.equal(harness.invoker.getAttribute("aria-hidden"), null);
  assert.equal(harness.wrapper.getAttribute("aria-hidden"), "true");
  assert.equal(harness.documentObject.activeElement, harness.invoker);
  assert.equal(harness.controller.isOpen(), false);
});
