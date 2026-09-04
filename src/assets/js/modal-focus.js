(function (root, factory) {
  "use strict";

  if (typeof module === "object" && module.exports) {
    module.exports = factory;
  } else if (root && root.document) {
    root.sundownModalFocus = factory(root);
  }
}(typeof window !== "undefined" ? window : this, function (windowObject) {
  "use strict";

  var documentObject = windowObject.document;
  var openControllers = [];
  var backgroundState = null;
  var focusableSelector = [
    "a[href]",
    "button:not([disabled])",
    "input:not([disabled])",
    "select:not([disabled])",
    "textarea:not([disabled])",
    "[tabindex]:not([tabindex='-1'])"
  ].join(",");

  function rememberElement(element) {
    var knownState = backgroundState.some(function (state) {
      return state.element === element;
    });
    if (knownState) return;

    backgroundState.push({
      element: element,
      inert: element.hasAttribute("inert"),
      ariaHidden: element.getAttribute("aria-hidden")
    });
  }

  function restoreAttribute(element, name, value) {
    if (value === null || value === false) {
      element.removeAttribute(name);
    } else {
      element.setAttribute(name, value === true ? "" : value);
    }
  }

  function updateBackground() {
    if (backgroundState) {
      backgroundState.forEach(function (state) {
        restoreAttribute(state.element, "inert", state.inert);
        restoreAttribute(state.element, "aria-hidden", state.ariaHidden);
      });
    }

    if (!openControllers.length) {
      backgroundState = null;
      return;
    }

    if (!backgroundState) backgroundState = [];
    var activeElement = openControllers[openControllers.length - 1].dialog;
    while (activeElement && activeElement !== documentObject.body) {
      var parent = activeElement.parentElement;
      if (!parent) break;

      rememberElement(activeElement);
      activeElement.removeAttribute("inert");
      activeElement.removeAttribute("aria-hidden");
      Array.prototype.forEach.call(parent.children, function (sibling) {
        if (sibling === activeElement) return;
        rememberElement(sibling);
        sibling.setAttribute("inert", "");
        sibling.setAttribute("aria-hidden", "true");
      });
      activeElement = parent;
    }
  }

  function focusableElements(dialog) {
    return Array.prototype.filter.call(dialog.querySelectorAll(focusableSelector), function (element) {
      var candidate = element;
      while (candidate && candidate !== dialog) {
        if (candidate.hidden || candidate.hasAttribute("inert") || candidate.getAttribute("aria-hidden") === "true") {
          return false;
        }
        candidate = candidate.parentElement;
      }
      return !dialog.hidden && !dialog.hasAttribute("inert") && dialog.getAttribute("aria-hidden") !== "true";
    });
  }

  function createController(dialog) {
    var invokingElement = null;
    var controller = {
      dialog: dialog,

      open: function (invoker, initialFocus) {
        if (openControllers.indexOf(controller) === -1) {
          invokingElement = invoker && typeof invoker.focus === "function" ? invoker : null;
          openControllers.push(controller);
          updateBackground();
        }

        var target = initialFocus || focusableElements(dialog)[0] || dialog;
        if (target && typeof target.focus === "function") target.focus();
      },

      close: function () {
        var index = openControllers.indexOf(controller);
        if (index === -1) return;

        openControllers.splice(index, 1);
        updateBackground();
        if (invokingElement && invokingElement.isConnected !== false) invokingElement.focus();
        invokingElement = null;
      },

      isOpen: function () {
        return openControllers.indexOf(controller) !== -1;
      },

      isTop: function () {
        return openControllers[openControllers.length - 1] === controller;
      },

      trapTab: function (event) {
        if (!controller.isTop() || event.key !== "Tab") return false;

        var elements = focusableElements(dialog);
        var first = elements[0] || dialog;
        var last = elements[elements.length - 1] || dialog;
        var active = documentObject.activeElement;
        var outside = !dialog.contains(active);

        if ((event.shiftKey && (active === first || outside)) || (!event.shiftKey && (active === last || outside))) {
          event.preventDefault();
          (event.shiftKey ? last : first).focus();
          return true;
        }

        return false;
      }
    };

    return controller;
  }

  return { createController: createController };
}));
