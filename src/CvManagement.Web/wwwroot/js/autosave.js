(function () {
  "use strict";

  var FLUSH_INTERVAL_MS = 7000; // spec: every 5-10s, not per-keystroke
  var RETRY_INTERVAL_MS = 15000; // after a network/server failure
  var dirty = new Set(); // attributeIds edited since their last successful send
  var timer = null;
  var inFlight = null; // Promise of the request currently on the wire, if any
  var endpoint = window.AUTOSAVE_ENDPOINT || "/api/profile/autosave";
  var text = Object.assign({
    unsaved: "Unsaved changes...", saving: "Saving...", saved: "Saved", conflict: "Conflict",
    failed: "Save failed", offline: "Offline -- will retry"
  }, window.AUTOSAVE_TEXT || {});

  function antiforgeryToken() {
    var el = document.querySelector('input[name="__RequestVerificationToken"]');
    return el ? el.value : "";
  }

  function fieldWrapper(attributeId) {
    return document.querySelector('.autosave-field[data-attribute-id="' + attributeId + '"]');
  }

  function readValue(wrapper) {
    var input = wrapper.querySelector(".autosave-input");
    switch (wrapper.dataset.type) {
      case "0": return { valueString: input.value };                       // String
      case "1": return { valueText: input.value };                         // Text
      case "2": return { valueImageUrl: input.value || null };             // Image
      case "3": return { valueNumeric: input.value === "" ? null : parseFloat(input.value) }; // Numeric
      case "4": return { valueDate: input.value || null };                 // Date
      case "5": return {                                                   // Period
        valuePeriodStart: wrapper.querySelector(".period-start").value || null,
        valuePeriodEnd: wrapper.querySelector(".period-end").value || null
      };
      case "6": return { valueBoolean: input.checked };                    // Boolean
      case "7": return { valueOptionId: input.value === "" ? null : parseInt(input.value, 10) }; // OneOfMany
      default: return {};
    }
  }

  // Value AND row version are read when the batch is built, never when the key was pressed: a field
  // edited while an earlier save is in flight must be sent with the version that save returns,
  // otherwise it would conflict with the user's own previous write.
  function buildBatch() {
    return Array.from(dirty).map(function (attributeId) {
      var wrapper = fieldWrapper(attributeId);
      return Object.assign({ attributeId: attributeId, rowVersion: wrapper.dataset.rowVersion || null }, readValue(wrapper));
    });
  }

  function setStatus(wrapper, message, isError) {
    var status = wrapper.querySelector(".autosave-status");
    if (!status) return;
    status.textContent = message;
    status.className = "autosave-status small " + (isError ? "text-danger" : "text-secondary");
  }

  function setEmpty(wrapper, isEmpty) {
    wrapper.dataset.empty = isEmpty ? "true" : "false";
    // Optional fields may stay empty: never highlighted in red.
    var needsValue = isEmpty && wrapper.dataset.optional !== "true";
    var badge = wrapper.querySelector(".autosave-empty-badge");
    if (badge) badge.hidden = !needsValue;
    wrapper.querySelectorAll(".autosave-input:not([type=checkbox]):not([type=hidden]), .period-start").forEach(function (el) {
      el.classList.toggle("border-danger", needsValue);
    });
    refreshPublishButton();
  }

  // CV page: "Publish" is only available once every required field is filled -- kept in sync live, so
  // filling the last red field enables it without a reload. Optional fields never block it.
  function refreshPublishButton() {
    var button = document.getElementById("publishButton");
    if (!button) return;
    var anyEmpty = document.querySelector('.autosave-field[data-empty="true"]:not([data-optional="true"])') !== null;
    button.disabled = anyEmpty;
    var hint = document.getElementById("publishHint");
    if (hint) {
      hint.textContent = anyEmpty ? hint.dataset.textIncomplete : hint.dataset.textReady;
      hint.closest(".alert").classList.toggle("alert-success", !anyEmpty);
      hint.closest(".alert").classList.toggle("alert-warning", anyEmpty);
    }
  }

  function schedule(delay) {
    if (!timer) timer = setTimeout(flush, delay);
  }

  function markDirty(attributeId) {
    var wrapper = fieldWrapper(attributeId);
    if (!wrapper) return;
    dirty.add(attributeId);
    setStatus(wrapper, text.unsaved, false);
    schedule(FLUSH_INTERVAL_MS);
  }

  function showConflict(wrapper, result) {
    var banner = wrapper.querySelector(".autosave-conflict");
    if (!banner) return;
    banner.hidden = false;
    banner.querySelector(".autosave-conflict-keep").onclick = function () {
      wrapper.dataset.rowVersion = result.currentRowVersion; // overwrite deliberately, with the fresh version
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
      setEmpty(wrapper, result.isEmpty);
      // Edited again while this save was in flight: keep the "unsaved" status, the next flush sends it.
      if (!dirty.has(result.attributeId)) setStatus(wrapper, text.saved, false);
    } else if (result.status === "conflict") {
      dirty.delete(result.attributeId); // needs a user decision, not an automatic retry
      setStatus(wrapper, text.conflict, true);
      showConflict(wrapper, result);
    } else {
      dirty.delete(result.attributeId); // validation error: retrying the same value can't succeed
      setStatus(wrapper, result.errorMessage || text.failed, true);
    }
  }

  function flush() {
    timer = null;
    if (inFlight) return inFlight.then(flush);
    if (dirty.size === 0) return Promise.resolve();

    var batch = buildBatch();
    dirty.clear();
    batch.forEach(function (c) { setStatus(fieldWrapper(c.attributeId), text.saving, false); });

    inFlight = fetch(endpoint, {
      method: "POST",
      headers: { "Content-Type": "application/json", "RequestVerificationToken": antiforgeryToken() },
      body: JSON.stringify({ changes: batch })
    })
      .then(function (r) {
        if (!r.ok) throw { status: r.status };
        return r.json();
      })
      .then(function (data) {
        (data.results || []).forEach(applyResult);
      })
      .catch(function (err) {
        var retryable = !err || !err.status || err.status >= 500 || err.status === 408 || err.status === 429;
        batch.forEach(function (c) {
          var wrapper = fieldWrapper(c.attributeId);
          if (retryable) {
            dirty.add(c.attributeId); // nothing is lost: re-queued, and the current DOM value is re-read next time
            setStatus(wrapper, text.offline, true);
          } else {
            setStatus(wrapper, text.failed + " (" + err.status + ")", true);
          }
        });
        if (retryable) schedule(RETRY_INTERVAL_MS);
      })
      .finally(function () {
        inFlight = null;
        if (dirty.size > 0) schedule(FLUSH_INTERVAL_MS);
      });
    return inFlight;
  }

  window.ProfileAutoSave = { markDirty: markDirty, flushNow: flush };

  // "Save changes" button (_AutoSaveBar): saves pending edits right away instead of waiting for the
  // timer, then reports the outcome. Texts come from data-* attributes so they're localized.
  document.addEventListener("click", function (e) {
    var button = e.target.closest(".autosave-save-btn");
    if (!button) return;
    var bar = button.closest(".autosave-bar");
    var status = bar.querySelector(".autosave-save-status");
    function show(text, kind) {
      status.textContent = text;
      status.className = "autosave-save-status small text-" + kind;
    }

    button.disabled = true;
    show(bar.dataset.textSaving, "secondary");
    if (timer) { clearTimeout(timer); timer = null; }
    flush().then(function () {
      // Failed saves are re-queued (dirty) and field errors/conflicts are marked in red on the field.
      var failed = dirty.size > 0 || document.querySelector(".autosave-status.text-danger") !== null;
      show(failed ? bar.dataset.textFailed : bar.dataset.textSaved, failed ? "danger" : "success");
    }).finally(function () {
      button.disabled = false;
    });
  });

  function onEdit(e) {
    var wrapper = e.target.closest(".autosave-field");
    if (wrapper) markDirty(parseInt(wrapper.dataset.attributeId, 10));
  }
  document.addEventListener("input", onEdit);
  document.addEventListener("change", onEdit);

  // Publishing (or any form marked data-autosave-flush) first flushes pending edits, so the server
  // judges completeness on what the user actually typed, not on what the last timer tick sent.
  document.addEventListener("submit", function (e) {
    var form = e.target;
    if (!form.hasAttribute("data-autosave-flush") || (dirty.size === 0 && !inFlight)) return;
    e.preventDefault();
    flush().then(function () { form.submit(); });
  });

  // Last-chance save on navigation. fetch+keepalive (unlike sendBeacon) can carry the antiforgery
  // header, so the request is actually accepted by the server.
  window.addEventListener("pagehide", function () {
    if (dirty.size === 0) return;
    fetch(endpoint, {
      method: "POST",
      keepalive: true,
      headers: { "Content-Type": "application/json", "RequestVerificationToken": antiforgeryToken() },
      body: JSON.stringify({ changes: buildBatch() })
    });
  });

  document.addEventListener("DOMContentLoaded", refreshPublishButton);
})();
