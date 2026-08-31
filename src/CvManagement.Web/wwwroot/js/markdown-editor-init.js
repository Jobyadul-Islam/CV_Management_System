(function () {
  "use strict";

  document.addEventListener("DOMContentLoaded", function () {
    if (typeof EasyMDE === "undefined") return;

    Array.prototype.forEach.call(document.querySelectorAll(".markdown-editor"), function (textarea) {
      var easyMDE = new EasyMDE({
        element: textarea,
        spellChecker: false,
        status: false,
        toolbar: ["bold", "italic", "heading", "|", "quote", "unordered-list", "ordered-list", "link", "|", "preview"]
      });

      // EasyMDE keeps the underlying <textarea>.value in sync but does not fire native input/change
      // events on it -- autosave.js listens for those, so re-dispatch one whenever EasyMDE changes.
      easyMDE.codemirror.on("change", function () {
        textarea.value = easyMDE.value();
        textarea.dispatchEvent(new Event("input", { bubbles: true }));
      });
    });
  });
})();
