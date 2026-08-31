(function () {
  "use strict";

  var confirmCallbacks = {};

  window.AttributePicker = {
    /** Register a callback(selectedAttrs[]) invoked when "Add selected" is clicked for #modalId. */
    onConfirm: function (modalId, callback) {
      confirmCallbacks[modalId] = callback;
    },
    recordUsage: function (attributeId) {
      var tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
      if (!tokenInput) return;
      fetch("/Attributes/RecordUsage", {
        method: "POST",
        headers: { "Content-Type": "application/x-www-form-urlencoded" },
        body: "attributeId=" + encodeURIComponent(attributeId) + "&__RequestVerificationToken=" + encodeURIComponent(tokenInput.value)
      });
    }
  };

  function filterList(modal) {
    var search = (modal.querySelector(".attribute-picker-search").value || "").toLowerCase();
    var categoryId = modal.querySelector(".attribute-picker-category").value;

    Array.prototype.forEach.call(modal.querySelectorAll(".attribute-picker-row"), function (row) {
      var nameMatches = row.dataset.name.indexOf(search) === 0 || search === "";
      var categoryMatches = categoryId === "" || row.dataset.categoryId === categoryId;
      row.style.display = (nameMatches && categoryMatches) ? "" : "none";
    });
  }

  function wireModal(modal) {
    var searchInput = modal.querySelector(".attribute-picker-search");
    var categorySelect = modal.querySelector(".attribute-picker-category");
    if (searchInput) searchInput.addEventListener("input", function () { filterList(modal); });
    if (categorySelect) categorySelect.addEventListener("change", function () { filterList(modal); });

    var confirmBtn = modal.querySelector(".attribute-picker-confirm");
    if (confirmBtn) {
      confirmBtn.addEventListener("click", function () {
        var selected = Array.prototype.map.call(
          modal.querySelectorAll(".form-check-input:checked"),
          function (cb) {
            return {
              id: parseInt(cb.value, 10),
              name: cb.dataset.nameDisplay,
              categoryName: cb.dataset.categoryName,
              dataType: parseInt(cb.dataset.type, 10)
            };
          }
        );

        var callback = confirmCallbacks[modal.id];
        if (callback && selected.length > 0) callback(selected);
        selected.forEach(function (attr) { window.AttributePicker.recordUsage(attr.id); });

        Array.prototype.forEach.call(modal.querySelectorAll(".form-check-input:checked"), function (cb) { cb.checked = false; });

        var bsModal = bootstrap.Modal.getInstance(modal);
        if (bsModal) bsModal.hide();
      });
    }
  }

  document.addEventListener("DOMContentLoaded", function () {
    Array.prototype.forEach.call(document.querySelectorAll(".modal"), function (modal) {
      if (modal.querySelector(".attribute-picker-list")) wireModal(modal);
    });
  });
})();
