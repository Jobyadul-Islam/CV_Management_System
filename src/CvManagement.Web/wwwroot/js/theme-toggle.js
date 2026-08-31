(function () {
  "use strict";

  function setCookie(name, value, days) {
    var expires = new Date(Date.now() + days * 864e5).toUTCString();
    document.cookie = name + "=" + value + "; expires=" + expires + "; path=/; SameSite=Lax";
  }

  function applyTheme(theme) {
    document.documentElement.setAttribute("data-bs-theme", theme);
    var icon = document.getElementById("theme-toggle-icon");
    if (icon) {
      icon.textContent = theme === "dark" ? "☀️" : "\u{1F319}";
    }
  }

  document.addEventListener("DOMContentLoaded", function () {
    var toggle = document.getElementById("theme-toggle-btn");
    if (!toggle) return;

    toggle.addEventListener("click", function () {
      var current = document.documentElement.getAttribute("data-bs-theme") || "light";
      var next = current === "dark" ? "light" : "dark";
      applyTheme(next);
      setCookie("CvManagement.Theme", next, 365);
    });
  });
})();
