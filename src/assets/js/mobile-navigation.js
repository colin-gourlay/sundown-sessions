(function (root, factory) {
  "use strict";

  if (typeof module === "object" && module.exports) {
    module.exports = factory;
  } else if (root && root.document) {
    root.sundownMobileNavigation = factory(root);
  }
}(typeof window !== "undefined" ? window : this, function (windowObject) {
  "use strict";

  var documentObject = windowObject.document;
  var openButton = documentObject.getElementById("mobile-menu-open");
  var dialog = documentObject.getElementById("mobile-navigation");
  if (!openButton || !dialog || !windowObject.sundownModalFocus) return null;

  var closeButton = dialog.querySelector("[data-mobile-menu-close]");
  var submenuButtons = Array.prototype.slice.call(dialog.querySelectorAll("[data-mobile-submenu-toggle]"));
  var modalFocus = windowObject.sundownModalFocus.createController(dialog);

  function setSubmenu(button, expanded) {
    var panel = documentObject.getElementById(button.getAttribute("aria-controls"));
    if (!panel) return;

    button.setAttribute("aria-expanded", String(expanded));
    panel.setAttribute("data-open", String(expanded));
    panel.setAttribute("aria-hidden", String(!expanded));
    if (expanded) panel.removeAttribute("inert");
    else panel.setAttribute("inert", "");
  }

  function openMenu() {
    if (modalFocus.isOpen()) return;

    dialog.setAttribute("data-open", "true");
    dialog.setAttribute("aria-hidden", "false");
    dialog.setAttribute("aria-modal", "true");
    dialog.removeAttribute("inert");
    openButton.setAttribute("aria-expanded", "true");
    modalFocus.open(openButton, closeButton || dialog);
  }

  function closeMenu() {
    if (!modalFocus.isOpen()) return;

    dialog.setAttribute("data-open", "false");
    openButton.setAttribute("aria-expanded", "false");
    modalFocus.close();
    dialog.setAttribute("aria-hidden", "true");
    dialog.removeAttribute("aria-modal");
    dialog.setAttribute("inert", "");
  }

  openButton.addEventListener("click", openMenu);
  if (closeButton) closeButton.addEventListener("click", closeMenu);
  submenuButtons.forEach(function (button) {
    button.addEventListener("click", function () {
      setSubmenu(button, button.getAttribute("aria-expanded") !== "true");
    });
  });
  documentObject.addEventListener("keydown", function (event) {
    if (event.key === "Tab") modalFocus.trapTab(event);
    if (event.key === "Escape" && modalFocus.isTop()) {
      event.preventDefault();
      closeMenu();
    }
  });

  return {
    close: closeMenu,
    open: openMenu,
    setSubmenu: setSubmenu
  };
}));
