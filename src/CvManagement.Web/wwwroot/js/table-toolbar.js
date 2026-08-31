(function () {
  "use strict";

  function initTable(table) {
    var toolbar = document.getElementById(table.dataset.toolbar);
    if (!toolbar) return;

    var selectAll = table.querySelector(".select-all");
    var selected = new Set();

    function rowCheckboxes() {
      return Array.prototype.slice.call(table.querySelectorAll(".row-select"));
    }

    function updateToolbar() {
      var countEl = toolbar.querySelector(".selection-count");
      if (countEl) countEl.textContent = selected.size + " selected";

      Array.prototype.forEach.call(toolbar.querySelectorAll(".toolbar-action"), function (btn) {
        var min = parseInt(btn.dataset.minSelected || "0", 10);
        var maxAttr = btn.dataset.maxSelected;
        var max = maxAttr ? parseInt(maxAttr, 10) : Infinity;
        btn.disabled = !(selected.size >= min && selected.size <= max);
      });
    }

    function toggleRow(cb) {
      var tr = cb.closest("tr");
      if (cb.checked) {
        selected.add(cb.value);
        if (tr) tr.classList.add("row-selected");
      } else {
        selected.delete(cb.value);
        if (tr) tr.classList.remove("row-selected");
      }
      updateToolbar();
    }

    rowCheckboxes().forEach(function (cb) {
      cb.addEventListener("change", function () { toggleRow(cb); });
    });

    if (selectAll) {
      selectAll.addEventListener("change", function () {
        rowCheckboxes().forEach(function (cb) {
          cb.checked = selectAll.checked;
          toggleRow(cb);
        });
      });
    }

    Array.prototype.forEach.call(toolbar.querySelectorAll(".toolbar-action"), function (btn) {
      btn.addEventListener("click", function () {
        if (btn.disabled) return;
        var ids = Array.from(selected);
        if (btn.dataset.confirm && !window.confirm(btn.dataset.confirm)) return;

        if (btn.dataset.mode === "get") {
          window.location.href = btn.dataset.url.replace("{id}", ids[0]);
        } else if (btn.dataset.mode === "post") {
          var form = document.getElementById(table.dataset.toolbar + "-post-form");
          if (!form) return;
          form.action = btn.dataset.url;
          Array.prototype.forEach.call(form.querySelectorAll("input[name='ids']"), function (el) { el.remove(); });
          ids.forEach(function (id) {
            var input = document.createElement("input");
            input.type = "hidden";
            input.name = "ids";
            input.value = id;
            form.appendChild(input);
          });
          form.submit();
        }
      });
    });

    updateToolbar();
  }

  document.addEventListener("DOMContentLoaded", function () {
    Array.prototype.forEach.call(document.querySelectorAll(".selectable-table[data-toolbar]"), initTable);
  });
})();
