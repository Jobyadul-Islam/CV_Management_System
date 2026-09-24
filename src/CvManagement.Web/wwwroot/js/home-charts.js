(function () {
  "use strict";

  // Home-page statistics charts (Chart.js). Colors are read from Bootstrap's CSS variables at render
  // time, so light and dark mode each get their own values, and re-read when theme-toggle.js
  // dispatches "themechange".
  if (typeof Chart === "undefined") return;

  function themeColors() {
    var css = getComputedStyle(document.documentElement);
    function v(name) { return css.getPropertyValue(name).trim(); }
    return {
      bar: v("--bs-primary"),
      text: v("--bs-secondary-color"),
      grid: v("--bs-border-color-translucent"),
      surface: v("--bs-body-bg"),
      ink: v("--bs-body-color")
    };
  }

  function options(colors, horizontal) {
    var valueAxis = {
      beginAtZero: true,
      ticks: { precision: 0, color: colors.text },   // counts: whole numbers only
      grid: { color: colors.grid },                   // recessive grid on the value axis only
      border: { display: false }
    };
    var categoryAxis = {
      ticks: { color: colors.text, autoSkip: true, maxRotation: 0 },
      grid: { display: false },
      border: { color: colors.grid }
    };
    return {
      indexAxis: horizontal ? "y" : "x",
      responsive: true,
      maintainAspectRatio: false,
      animation: false,
      plugins: {
        legend: { display: false }, // single series: the card heading names it
        tooltip: {
          backgroundColor: colors.surface,
          titleColor: colors.ink,
          bodyColor: colors.ink,
          borderColor: colors.grid,
          borderWidth: 1,
          displayColors: false
        }
      },
      interaction: { mode: "index", intersect: false }, // hover target = the whole band, not just the bar
      scales: horizontal ? { x: valueAxis, y: categoryAxis } : { x: categoryAxis, y: valueAxis }
    };
  }

  var charts = [];

  document.querySelectorAll("canvas.home-chart").forEach(function (canvas) {
    var points = JSON.parse(canvas.dataset.points || "[]");
    var horizontal = canvas.dataset.horizontal === "true";
    var colors = themeColors();

    var chart = new Chart(canvas, {
      type: "bar",
      data: {
        labels: points.map(function (p) { return p.Label; }),
        datasets: [{
          label: canvas.dataset.seriesLabel,
          data: points.map(function (p) { return p.Value; }),
          backgroundColor: colors.bar,
          borderRadius: 4,          // rounded data end...
          borderSkipped: "start",   // ...square where the bar meets the baseline
          maxBarThickness: 28,
          categoryPercentage: 0.9,
          barPercentage: 0.9        // leaves a surface gap between adjacent bars
        }]
      },
      options: options(colors, horizontal)
    });
    charts.push({ chart: chart, horizontal: horizontal });
  });

  document.addEventListener("themechange", function () {
    var colors = themeColors();
    charts.forEach(function (entry) {
      entry.chart.data.datasets[0].backgroundColor = colors.bar;
      entry.chart.options = options(colors, entry.horizontal);
      entry.chart.update();
    });
  });
})();
