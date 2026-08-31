(function () {
  "use strict";

  document.addEventListener("DOMContentLoaded", function () {
    var buttons = document.querySelectorAll(".cloudinary-upload-btn");
    if (buttons.length === 0 || typeof cloudinary === "undefined") return;

    Array.prototype.forEach.call(buttons, function (btn) {
      var container = btn.closest(".autosave-field") || btn.parentElement;
      var hiddenInput = container.querySelector(".image-url-input");
      var preview = container.querySelector(".image-preview");

      var widget = cloudinary.createUploadWidget(
        {
          cloudName: btn.dataset.cloudName,
          uploadPreset: btn.dataset.uploadPreset,
          sources: ["local", "url", "camera"],
          multiple: false,
          cropping: true,
          croppingAspectRatio: 1
        },
        function (error, result) {
          if (error || !result || result.event !== "success") return;
          var url = result.info.secure_url;
          hiddenInput.value = url;
          preview.src = url;
          preview.style.display = "";
          hiddenInput.dispatchEvent(new Event("input", { bubbles: true }));
        }
      );

      btn.addEventListener("click", function () { widget.open(); });
    });
  });
})();
