const assert = require("node:assert/strict");
const test = require("node:test");

const createPlayer = require("../../src/assets/js/listen-live-player.js");

class FakeEventTarget {
  constructor() {
    this.listeners = new Map();
  }

  addEventListener(name, handler) {
    const handlers = this.listeners.get(name) || [];
    handlers.push(handler);
    this.listeners.set(name, handlers);
  }

  removeEventListener(name, handler) {
    const handlers = this.listeners.get(name) || [];
    this.listeners.set(name, handlers.filter((candidate) => candidate !== handler));
  }

  dispatch(name, properties = {}) {
    const event = { type: name, ...properties };
    const handlers = this.listeners.get(name) || [];
    handlers.slice().forEach((handler) => handler(event));
  }
}

class FakeElement extends FakeEventTarget {
  constructor() {
    super();
    this.attributes = new Map();
    this.children = new Map();
    this.hidden = false;
    this.textContent = "";
  }

  getAttribute(name) {
    return this.attributes.has(name) ? this.attributes.get(name) : null;
  }

  setAttribute(name, value) {
    this.attributes.set(name, String(value));
  }

  removeAttribute(name) {
    this.attributes.delete(name);
  }

  querySelector(selector) {
    return this.children.get(selector) || null;
  }
}

class FakeAudio extends FakeElement {
  constructor(source, documentObject) {
    super();
    this.children.set("source", source);
    this.documentObject = documentObject;
    this.currentTime = 0;
    this.ended = false;
    this.error = null;
    this.loadCalls = 0;
    this.paused = true;
    this.playCalls = 0;
    this.playImplementation = null;
  }

  focus() {
    this.documentObject.activeElement = this;
  }

  load() {
    this.loadCalls += 1;
    this.paused = true;
  }

  play() {
    this.playCalls += 1;

    if (this.playImplementation) {
      return this.playImplementation();
    }

    this.paused = false;
    this.dispatch("play");
    return Promise.resolve();
  }
}

class FakeClock {
  constructor() {
    this.currentTime = 0;
    this.nextId = 1;
    this.tasks = new Map();
  }

  clearInterval(id) {
    this.tasks.delete(id);
  }

  clearTimeout(id) {
    this.tasks.delete(id);
  }

  setInterval(callback, delay) {
    return this.addTask(callback, delay, delay);
  }

  setTimeout(callback, delay) {
    return this.addTask(callback, delay, null);
  }

  pendingTimeoutDelays() {
    return Array.from(this.tasks.values())
      .filter((task) => task.interval === null)
      .map((task) => task.time - this.currentTime)
      .sort((left, right) => left - right);
  }

  addTask(callback, delay, interval) {
    const id = this.nextId;
    this.nextId += 1;
    this.tasks.set(id, {
      callback,
      interval,
      time: this.currentTime + delay
    });
    return id;
  }

  tick(duration) {
    const targetTime = this.currentTime + duration;

    while (true) {
      let nextId = null;
      let nextTask = null;

      for (const [id, task] of this.tasks) {
        if (task.time <= targetTime &&
            (!nextTask || task.time < nextTask.time ||
              (task.time === nextTask.time && id < nextId))) {
          nextId = id;
          nextTask = task;
        }
      }

      if (!nextTask) {
        break;
      }

      this.currentTime = nextTask.time;
      this.tasks.delete(nextId);
      if (nextTask.interval !== null) {
        this.tasks.set(nextId, {
          ...nextTask,
          time: nextTask.time + nextTask.interval
        });
      }
      nextTask.callback();
    }

    this.currentTime = targetTime;
  }
}

function createHarness() {
  const clock = new FakeClock();
  const documentObject = {
    activeElement: null,
    hidden: false,
    querySelectorAll: () => []
  };
  const windowObject = new FakeEventTarget();
  const root = new FakeElement();
  const source = new FakeElement();
  const status = new FakeElement();
  const retryButton = new FakeElement();
  const audio = new FakeAudio(source, documentObject);

  windowObject.document = documentObject;
  windowObject.location = { href: "https://sundownsessions.co.uk/listen-live/" };
  windowObject.navigator = { onLine: true };
  windowObject.URL = URL;
  windowObject.clearInterval = clock.clearInterval.bind(clock);
  windowObject.clearTimeout = clock.clearTimeout.bind(clock);
  windowObject.setInterval = clock.setInterval.bind(clock);
  windowObject.setTimeout = clock.setTimeout.bind(clock);

  root.setAttribute("data-stream-url", "https://radio.example.test/stream?mount=main");
  root.children.set("[data-listen-live-player-audio]", audio);
  root.children.set("[data-listen-live-player-status]", status);
  root.children.set("[data-listen-live-player-retry]", retryButton);

  const api = createPlayer(windowObject);
  const controller = api.createController(root, {
    bufferGraceMs: 12000,
    clock,
    connectTimeoutMs: 20000,
    now: () => clock.currentTime,
    progressIntervalMs: 5000,
    progressStallMs: 20000,
    retryDelaysMs: [1000, 2000, 5000, 10000, 20000, 30000],
    stablePlaybackMs: 30000
  });

  return {
    audio,
    clock,
    controller,
    root,
    retryButton,
    source,
    status,
    windowObject
  };
}

function startPlayback(harness) {
  harness.audio.paused = false;
  harness.audio.dispatch("play");
  harness.audio.dispatch("playing");
}

async function flushPromises() {
  await Promise.resolve();
  await Promise.resolve();
}

test("initialisation does not start playback", () => {
  const harness = createHarness();

  assert.ok(harness.controller);
  assert.equal(harness.audio.loadCalls, 0);
  assert.equal(harness.audio.playCalls, 0);
  assert.equal(harness.root.getAttribute("data-player-state"), "idle");
  assert.equal(harness.status.textContent, "Press play to listen live.");
  assert.equal(harness.retryButton.hidden, true);
});

test("browser bootstrap initialises each player only once", () => {
  const harness = createHarness();
  harness.controller.destroy();
  harness.windowObject.document.querySelectorAll = () => [harness.root];
  const api = createPlayer(harness.windowObject);

  assert.equal(api.init().length, 1);
  assert.equal(api.init().length, 0);

  harness.audio.paused = false;
  harness.audio.dispatch("play");
  assert.equal(harness.root.getAttribute("data-player-state"), "connecting");
});

test("brief buffering is tolerated and playing cancels recovery", () => {
  const harness = createHarness();

  startPlayback(harness);
  harness.audio.dispatch("waiting");
  harness.clock.tick(11000);

  assert.equal(harness.audio.loadCalls, 0);
  assert.equal(harness.root.getAttribute("data-player-state"), "playing");

  harness.audio.currentTime = 5;
  harness.audio.dispatch("playing");
  harness.clock.tick(30000);

  assert.equal(harness.audio.loadCalls, 0);
  assert.equal(harness.root.getAttribute("data-player-state"), "playing");
});

test("a prolonged stall opens a fresh connection after the grace period", () => {
  const harness = createHarness();

  startPlayback(harness);
  harness.audio.dispatch("stalled");
  harness.clock.tick(12000);

  assert.equal(harness.root.getAttribute("data-player-state"), "retrying");
  assert.match(harness.status.textContent, /Reconnecting automatically/);
  assert.equal(harness.audio.loadCalls, 0);

  harness.clock.tick(1000);

  assert.equal(harness.audio.loadCalls, 1);
  assert.equal(harness.audio.playCalls, 1);
  assert.match(harness.source.getAttribute("src"), /mount=main&ss-reconnect=/);
});

test("an initial connection that hangs is recovered automatically", () => {
  const harness = createHarness();

  harness.audio.paused = false;
  harness.audio.dispatch("play");
  harness.clock.tick(20000);

  assert.equal(harness.root.getAttribute("data-player-state"), "retrying");
  assert.match(harness.status.textContent, /Reconnecting automatically/);

  harness.clock.tick(1000);

  assert.equal(harness.audio.loadCalls, 1);
  assert.equal(harness.audio.playCalls, 1);
});

test("the progress watchdog recovers a frozen stream after progress was observed", () => {
  const harness = createHarness();

  startPlayback(harness);
  harness.audio.currentTime = 5;
  harness.clock.tick(5000);
  harness.clock.tick(25000);

  assert.equal(harness.root.getAttribute("data-player-state"), "retrying");
  assert.match(harness.status.textContent, /Reconnecting automatically/);
});

test("a browser with a constant live-stream timeline is not force-reloaded", () => {
  const harness = createHarness();

  startPlayback(harness);
  harness.clock.tick(120000);

  assert.equal(harness.audio.loadCalls, 0);
  assert.equal(harness.root.getAttribute("data-player-state"), "playing");
});

test("duplicate failure events share one retry and use capped backoff", async () => {
  const harness = createHarness();
  const failures = [];
  const retryDelays = [];

  startPlayback(harness);
  harness.audio.playImplementation = () => {
    const error = new Error("network unavailable");
    error.name = "NetworkError";
    failures.push(error);
    return Promise.reject(error);
  };

  harness.audio.dispatch("error");
  harness.audio.dispatch("ended");
  harness.audio.dispatch("error");
  retryDelays.push(harness.clock.pendingTimeoutDelays()[0]);
  harness.clock.tick(1000);
  await flushPromises();
  retryDelays.push(harness.clock.pendingTimeoutDelays()[0]);

  for (const delay of [2000, 5000, 10000, 20000, 30000, 30000]) {
    harness.clock.tick(delay);
    await flushPromises();
    retryDelays.push(harness.clock.pendingTimeoutDelays()[0]);
  }

  assert.equal(harness.audio.playCalls, 7);
  assert.deepEqual(retryDelays, [1000, 2000, 5000, 10000, 20000, 30000, 30000, 30000]);
  assert.equal(failures.length, 7);
});

test("an intentional pause cancels pending and future automatic recovery", () => {
  const harness = createHarness();

  startPlayback(harness);
  harness.audio.error = { code: 2 };
  harness.audio.dispatch("error");
  harness.audio.paused = true;
  harness.audio.dispatch("pause");
  harness.clock.tick(60000);

  harness.windowObject.dispatch("online");
  harness.audio.dispatch("error");
  harness.clock.tick(60000);

  assert.equal(harness.audio.loadCalls, 0);
  assert.equal(harness.audio.playCalls, 0);
  assert.equal(harness.root.getAttribute("data-player-state"), "idle");
});

test("the automatic pause at the natural end retains recovery intent", () => {
  const harness = createHarness();

  startPlayback(harness);
  harness.audio.paused = true;
  harness.audio.ended = true;
  harness.audio.dispatch("pause");
  harness.audio.dispatch("ended");
  harness.clock.tick(1000);

  assert.equal(harness.audio.loadCalls, 1);
  assert.equal(harness.audio.playCalls, 1);
});

test("offline waits, then reconnects immediately when the network returns", () => {
  const harness = createHarness();

  startPlayback(harness);
  harness.windowObject.navigator.onLine = false;
  harness.windowObject.dispatch("offline");
  harness.clock.tick(60000);

  assert.equal(harness.audio.loadCalls, 0);
  assert.equal(harness.root.getAttribute("data-player-state"), "offline");

  harness.windowObject.navigator.onLine = true;
  harness.windowObject.dispatch("online");
  harness.clock.tick(0);

  assert.equal(harness.audio.loadCalls, 1);
  assert.equal(harness.audio.playCalls, 1);
});

test("pressing play while offline waits for the online event", () => {
  const harness = createHarness();

  harness.windowObject.navigator.onLine = false;
  harness.audio.paused = false;
  harness.audio.dispatch("play");
  harness.clock.tick(60000);

  assert.equal(harness.root.getAttribute("data-player-state"), "offline");
  assert.equal(harness.audio.loadCalls, 0);

  harness.windowObject.navigator.onLine = true;
  harness.windowObject.dispatch("online");
  harness.clock.tick(0);

  assert.equal(harness.audio.loadCalls, 1);
});

test("a blocked scripted play exposes a user-gesture retry without looping", async () => {
  const harness = createHarness();
  const notAllowed = new Error("user activation required");
  notAllowed.name = "NotAllowedError";

  startPlayback(harness);
  harness.audio.playImplementation = () => Promise.reject(notAllowed);
  harness.audio.dispatch("error");
  harness.clock.tick(1000);
  await flushPromises();
  harness.clock.tick(60000);

  assert.equal(harness.audio.playCalls, 1);
  assert.equal(harness.root.getAttribute("data-player-state"), "interaction-required");
  assert.equal(harness.retryButton.hidden, false);
  assert.match(harness.status.textContent, /needs you to restart playback/);

  harness.audio.playImplementation = null;
  harness.retryButton.dispatch("click");
  await flushPromises();

  assert.equal(harness.audio.playCalls, 2);
  assert.equal(harness.root.getAttribute("data-player-state"), "connecting");
  assert.equal(harness.retryButton.hidden, true);
});

test("an offline transition preserves an autoplay-blocked resume prompt", async () => {
  const harness = createHarness();
  const notAllowed = new Error("user activation required");
  notAllowed.name = "NotAllowedError";

  startPlayback(harness);
  harness.audio.playImplementation = () => Promise.reject(notAllowed);
  harness.audio.dispatch("error");
  harness.clock.tick(1000);
  await flushPromises();

  harness.windowObject.navigator.onLine = false;
  harness.windowObject.dispatch("offline");
  assert.equal(harness.root.getAttribute("data-player-state"), "offline");
  assert.equal(harness.retryButton.hidden, true);

  harness.windowObject.navigator.onLine = true;
  harness.windowObject.dispatch("online");

  assert.equal(harness.root.getAttribute("data-player-state"), "interaction-required");
  assert.equal(harness.retryButton.hidden, false);
  assert.equal(harness.audio.playCalls, 1);
});

test("a late play rejection cannot restart after a deliberate pause", async () => {
  const harness = createHarness();
  let rejectPlay;

  startPlayback(harness);
  harness.audio.playImplementation = () => new Promise((resolve, reject) => {
    rejectPlay = reject;
  });
  harness.audio.dispatch("error");
  harness.clock.tick(1000);

  harness.audio.paused = true;
  harness.audio.dispatch("pause");
  const lateError = new Error("late failure");
  lateError.name = "NetworkError";
  rejectPlay(lateError);
  await flushPromises();
  harness.clock.tick(60000);

  assert.equal(harness.audio.playCalls, 1);
  assert.equal(harness.audio.loadCalls, 1);
  assert.equal(harness.root.getAttribute("data-player-state"), "idle");
});
