(function () {
  "use strict";

  var container = document.getElementById("discussionPosts");
  if (!container || typeof signalR === "undefined") return;

  var positionId = parseInt(container.dataset.positionId, 10);
  var isRecruiter = container.dataset.isRecruiter === "true";

  function escapeHtml(text) {
    var div = document.createElement("div");
    div.textContent = text || "";
    return div.innerHTML;
  }

  function appendPost(post) {
    var wrapper = document.createElement("div");
    wrapper.className = "discussion-post";

    var authorName = escapeHtml(post.authorDisplayName);
    var authorHtml = (isRecruiter && post.authorUserId)
      ? '<a href="/Profile/View/' + encodeURIComponent(post.authorUserId) + '">' + authorName + '</a>'
      : authorName;

    var when = new Date(post.createdAtUtc + "Z").toLocaleString();

    wrapper.innerHTML =
      '<div class="d-flex justify-content-between">' +
        '<strong>' + authorHtml + '</strong>' +
        '<span class="text-secondary small">' + escapeHtml(when) + '</span>' +
      '</div>' +
      '<div>' + post.bodyHtml + '</div>'; // bodyHtml is server-side sanitized Markdown output

    container.appendChild(wrapper);
    wrapper.scrollIntoView({ behavior: "smooth", block: "end" });
  }

  var connection = new signalR.HubConnectionBuilder().withUrl("/hubs/discussion").withAutomaticReconnect().build();
  connection.on("NewPost", function (post) {
    if (post.id) appendPost(post);
  });
  connection.start().then(function () {
    connection.invoke("JoinPosition", positionId);
  });
  connection.onreconnected(function () {
    connection.invoke("JoinPosition", positionId);
  });

  var form = document.getElementById("discussionForm");
  if (form) {
    form.addEventListener("submit", function (e) {
      e.preventDefault();
      var textarea = document.getElementById("discussionBody");
      var body = textarea.value.trim();
      if (!body) return;

      var token = document.querySelector('input[name="__RequestVerificationToken"]').value;
      var submitBtn = form.querySelector("button[type=submit]");
      submitBtn.disabled = true;

      fetch("/Discussions/Post", {
        method: "POST",
        headers: { "Content-Type": "application/x-www-form-urlencoded" },
        body: "positionId=" + positionId + "&body=" + encodeURIComponent(body) + "&__RequestVerificationToken=" + encodeURIComponent(token)
      })
        .then(function (r) { if (r.ok) textarea.value = ""; })
        .finally(function () { submitBtn.disabled = false; });
    });
  }
})();
