(function () {
  "use strict";

  var FLUSH_INTERVAL_MS = 7000; // spec: every 5-10s, not per-keystroke
  var dirty = new Map(); // attributeId -> { rowVersion, payload }
  var timer = null;
  var inFlight = false;
  var endpoint = window.AUTOSAVE_ENDPOINT || "/api/profile/autosave";

  function antiforgeryToken() {
    var el = document.querySelector('input[name="__RequestVerificationToken"]');
    return el ? el.value : "";
  }

  function fieldWrapper(attributeId) {
    return document.querySelector('.autosave-field[data-attribute-id="' + attributeId + '"]');
  }

  function readValue(wrapper) {
    var type = wrapper.dataset.type;
    switch (type) {
      case "0": // String
        return { valueString: wrapper.querySelector(".autosave-input").value };
      case "1": // Text
        return { valueText: wrapper.querySelector(".autosave-input").value };
      case "2": // Image
        return { valueImageUrl: wrapper.querySelector(".autosave-input").value || null };
      case "3": { // Numeric
        var n = wrapper.querySelector(".autosave-input").value;
        return { valueNumeric: n === "" ? null : parseFloat(n) };
      }
      case "4": { // Date
        var d = wrapper.querySelector(".autosave-input").value;
        return { valueDate: d || null };
      }
      case "5": { // Period
        var s = wrapper.querySelector(".period-start").value;
        var e = wrapper.querySelector(".period-end").value;
        return { valuePeriodStart: s || null, valuePeriodEnd: e || null };
      }
      case "6": // Boolean
        return { valueBoolean: wrapper.querySelector(".autosave-input").checked };
      case "7": { // OneOfMany
        var o = wrapper.querySelector(".autosave-input").value;
        return { valueOptionId: o === "" ? null : parseInt(o, 10) };
      }
      default:
        return {};
    }
  }

  function setStatus(wrapper, text, isError) {
    var status = wrapper.querySelector(".autosave-status");
    if (!status) return;
    status.textContent = text;
    status.className = "autosave-status small " + (isError ? "text-danger" : "text-secondary");
  }

  function markDirty(attributeId) {
    var wrapper = fieldWrapper(attributeId);
    if (!wrapper) return;
    dirty.set(attributeId, { rowVersion: wrapper.dataset.rowVersion || null, payload: readValue(wrapper) });
    setStatus(wrapper, "Unsaved changes...", false);
    if (!timer) timer = setTimeout(flush, FLUSH_INTERVAL_MS);
  }

  function showConflict(wrapper, result) {
    var banner = wrapper.querySelector(".autosave-conflict");
    if (!banner) return;
    banner.hidden = false;
    banner.querySelector(".autosave-conflict-keep").onclick = function () {
      wrapper.dataset.rowVersion = result.currentRowVersion;
      banner.hidden = true;
      markDirty(parseInt(wrapper.dataset.attributeId, 10));
    };
    banner.querySelector(".autosave-conflict-reload").onclick = function () {
      window.location.reload();
    };
  }

  function applyResult(result) {
    var wrapper = fieldWrapper(result.attributeId);
    if (!wrapper) return;

    if (result.status === "ok") {
      wrapper.dataset.rowVersion = result.newRowVersion;
      setStatus(wrapper, "Saved", false);
    } else if (result.status === "conflict") {
      setStatus(wrapper, "Conflict", true);
      showConflict(wrapper, result);
    } else {
      setStatus(wrapper, result.errorMessage || "Save failed", true);
    }
  }

  function flush() {
    timer = null;
    if (dirty.size === 0 || inFlight) return;

    var batch = Array.from(dirty.entries()).map(function (entry) {
      var attributeId = entry[0], data = entry[1];
      return Object.assign({ attributeId: attributeId, rowVersion: data.rowVersion }, data.payload);
    });
    dirty.clear();
    inFlight = true;

    fetch(endpoint, {
      method: "POST",
      headers: { "Content-Type": "application/json", "RequestVerificationToken": antiforgeryToken() },
      body: JSON.stringify({ changes: batch })
    })
      .then(function (r) { return r.json(); })
      .then(function (data) {
        (data.results || []).forEach(applyResult);
      })
      .catch(function () {
        // Leave fields marked dirty implicitly by re-flagging status; next flush cycle retries.
        batch.forEach(function (c) {
          var wrapper = fieldWrapper(c.attributeId);
          if (wrapper) setStatus(wrapper, "Offline -- will retry", true);
        });
      })
      .finally(function () {
        inFlight = false;
        if (dirty.size > 0) timer = setTimeout(flush, FLUSH_INTERVAL_MS);
      });
  }

  window.ProfileAutoSave = { markDirty: markDirty, flushNow: flush };

  document.addEventListener("input", function (e) {
    var wrapper = e.target.closest(".autosave-field");
    if (wrapper) markDirty(parseInt(wrapper.dataset.attributeId, 10));
  });
  document.addEventListener("change", function (e) {
    var wrapper = e.target.closest(".autosave-field");
    if (wrapper) markDirty(parseInt(wrapper.dataset.attributeId, 10));
  });

  window.addEventListener("beforeunload", function () {
    if (dirty.size > 0 && !inFlight) {
      // Best-effort: browsers largely ignore async work here, but sendBeacon can carry small payloads.
      var batch = Array.from(dirty.entries()).map(function (entry) {
        return Object.assign({ attributeId: entry[0], rowVersion: entry[1].rowVersion }, entry[1].payload);
      });
      var blob = new Blob([JSON.stringify({ changes: batch })], { type: "application/json" });
      navigator.sendBeacon(endpoint, blob);
    }
  });
})();
