(function () {
  "use strict";

  var state = {
    payload: null,
    period: "12",
    theme: "light",
    density: "compact"
  };

  var ICONS = {
    bank: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M3 21h18M4 10h16M12 3l9 7H3z"/><path d="M8 10v11M16 10v11M12 10v11"/></svg>',
    cash: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="2" y="6" width="20" height="12" rx="2"/><circle cx="12" cy="12" r="3"/></svg>',
    income: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M12 19V5M6 11l6-6 6 6"/></svg>',
    expense: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M12 5v14M6 13l6 6 6-6"/></svg>',
    profit: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M4 19l6-6 4 4 6-8"/><path d="M14 9h6v6"/></svg>',
    people: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="9" cy="8" r="3"/><path d="M3 20v-1a5 5 0 0 1 10 0v1"/><circle cx="17" cy="9" r="2.5"/><path d="M21 20v-1a4 4 0 0 0-5-3.9"/></svg>',
    stock: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M3 7l9-4 9 4v10l-9 4-9-4z"/><path d="M12 11v10M3 7l9 4 9-4"/></svg>',
    journal: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M8 4h10a2 2 0 0 1 2 2v14H8z"/><path d="M6 4h2v16H6a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2z"/></svg>',
    invoice: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M7 3h8l5 5v13H7z"/><path d="M15 3v5h5M9 13h6M9 17h6"/></svg>',
    chart: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M4 19V5M4 19h16"/><path d="M8 15l3-4 3 2 5-7"/></svg>',
    receive: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="9"/><path d="M12 8v8M8 12h8"/></svg>',
    pay: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="2" y="7" width="20" height="12" rx="2"/><path d="M2 11h20"/></svg>',
    buy: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M6 7h15l-1.5 8H8L6 4H3"/><circle cx="9" cy="20" r="1.5"/><circle cx="18" cy="20" r="1.5"/></svg>',
    sale: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M4 7h16v13H4z"/><path d="M8 7V5a4 4 0 0 1 8 0v2"/></svg>',
    sun: '<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="4"/><path d="M12 2v2M12 20v2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M2 12h2M20 12h2M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4"/></svg>',
    moon: '<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 14.5A8.5 8.5 0 1 1 9.5 3 7 7 0 0 0 21 14.5z"/></svg>'
  };

  function $(id) { return document.getElementById(id); }

  function post(action, extra) {
    var msg = extra || {};
    msg.action = action;
    try {
      if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage(JSON.stringify(msg));
        return;
      }
    } catch (e) { }
    toast("این میانبر در محیط پیش‌نمایش فعال است.");
  }

  function toast(text) {
    var el = $("toast");
    el.textContent = text;
    el.style.display = "block";
    setTimeout(function () { el.style.display = "none"; }, 3200);
  }

  function fa(value) {
    if (value == null || value === "" || value === "—") return "—";
    return String(value).replace(/\d/g, function (d) {
      return "۰۱۲۳۴۵۶۷۸۹"[d];
    });
  }

  function esc(s) {
    return String(s == null ? "" : s)
      .replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
  }

  function applyChrome() {
    document.documentElement.setAttribute("data-theme", state.theme);
    document.documentElement.setAttribute("data-density", state.density || "compact");
    var scale = (state.fontScale || 100) / 100;
    document.documentElement.style.fontSize = (13 * scale) + "px";
    $("btnTheme").innerHTML = state.theme === "dark" ? ICONS.sun : ICONS.moon;
    $("btnTheme").setAttribute("aria-label", state.theme === "dark" ? "حالت روشن" : "حالت تیره");
  }

  function chartHeight() {
    var box = document.querySelector(".chart-box");
    if (box && box.clientHeight) return box.clientHeight;
    if (state.density === "spacious") return 240;
    if (state.density === "comfortable") return 200;
    return 168;
  }

  function renderKpis(kpis) {
    var html = "";
    (kpis || []).forEach(function (k) {
      var cls = k.trend === "down" ? "down" : (k.trend === "up" ? "up" : "");
      html += '<article class="card kpi">' +
        '<div class="kpi-icon ' + esc(k.icon) + '">' + (ICONS[k.icon] || ICONS.cash) + "</div>" +
        "<div><p class=\"kpi-label\">" + esc(k.label) + "</p>" +
        "<p class=\"kpi-value\">" + fa(k.value) +
        (k.unit ? "<span class=\"kpi-unit\">" + esc(k.unit) + "</span>" : "") + "</p>" +
        "<div class=\"kpi-change " + cls + "\">" + fa(k.change || "—") +
        "<span style=\"color:var(--color-text-secondary)\">" + esc(k.hint || "نسبت به ماه قبل") + "</span></div></div></article>";
    });
    $("kpiGrid").innerHTML = html || skeletonKpis();
  }

  function skeletonKpis() {
    return [1, 2, 3, 4, 5, 6].map(function () {
      return '<div class="card kpi"><div class="sk" style="width:42px;height:42px"></div><div style="width:100%"><div class="sk" style="height:12px;width:40%"></div><div class="sk" style="height:28px;width:70%;margin-top:10px"></div></div></div>';
    }).join("");
  }

  function sliceSeries(points, period) {
    var n = periodMonths(period);
    if (!points || !points.length) return [];
    if (!n || n >= points.length) return points;
    return points.slice(points.length - n);
  }

  function periodMonths(period) {
    if (period === "1" || period === "7" || period === "1m") return 1;
    var n = parseInt(period, 10);
    return n > 0 ? n : 12;
  }

  function lineChart(el, series, period) {
    var income = sliceSeries(series && series.income, period);
    var expense = sliceSeries(series && series.expense, period);
    if (!income.length) {
      el.innerHTML = empty("اطلاعاتی برای نمایش وجود ندارد.");
      return;
    }
    var w = el.clientWidth || 640;
    var h = chartHeight();
    var pad = { t: 18, r: 12, b: 36, l: 44 };
    var all = income.concat(expense).map(function (p) { return Number(p.value) || 0; });
    var max = Math.max.apply(null, all.concat([1]));
    var iw = w - pad.l - pad.r;
    var ih = h - pad.t - pad.b;
    var gridColor = isDark() ? "#263244" : "#E2E8F0";
    function x(i, len) { return pad.l + (len <= 1 ? iw / 2 : i * iw / (len - 1)); }
    function y(v) { return pad.t + ih - (v / max) * ih; }
    function path(arr) {
      return arr.map(function (p, i) {
        return (i ? "L" : "M") + x(i, arr.length).toFixed(1) + " " + y(Number(p.value) || 0).toFixed(1);
      }).join(" ");
    }
    var grid = "";
    for (var g = 0; g <= 4; g++) {
      var gy = pad.t + ih * g / 4;
      grid += '<line x1="' + pad.l + '" y1="' + gy + '" x2="' + (w - pad.r) + '" y2="' + gy + '" stroke="' + gridColor + '" stroke-width="1"/>';
    }
    var labels = income.map(function (p, i) {
      return '<text x="' + x(i, income.length) + '" y="' + (h - 10) + '" text-anchor="middle" font-size="11" fill="#94A3B8">' + esc(p.label) + "</text>";
    }).join("");
    var dots = income.map(function (p, i) {
      var iv = Number(p.value) || 0;
      var ev = expense[i] ? Number(expense[i].value) || 0 : 0;
      return '<circle cx="' + x(i, income.length) + '" cy="' + y(iv) + '" r="4" fill="#10B981"><title>' +
        esc(p.label) + " — درآمد " + fa(Math.round(iv)) + "</title></circle>" +
        '<circle cx="' + x(i, income.length) + '" cy="' + y(ev) + '" r="4" fill="#F97316"><title>' +
        esc(p.label) + " — هزینه " + fa(Math.round(ev)) + "</title></circle>";
    }).join("");
    el.innerHTML = '<svg viewBox="0 0 ' + w + " " + h + '" preserveAspectRatio="none" role="img" aria-label="نمودار درآمد و هزینه">' +
      grid +
      '<path d="' + path(income) + '" fill="none" stroke="#10B981" stroke-width="2.5" stroke-linejoin="round"/>' +
      '<path d="' + path(expense) + '" fill="none" stroke="#F97316" stroke-width="2.5" stroke-linejoin="round"/>' +
      dots + labels + "</svg>";
  }

  function barChart(el, series) {
    var points = series || [];
    if (!points.length) {
      el.innerHTML = empty("اطلاعاتی برای نمایش وجود ندارد.");
      return;
    }
    var w = el.clientWidth || 360;
    var h = chartHeight();
    var pad = { t: 16, r: 8, b: 36, l: 8 };
    var max = 1;
    points.forEach(function (p) {
      max = Math.max(max, Number(p.incoming) || 0, Number(p.outgoing) || 0);
    });
    var group = (w - pad.l - pad.r) / points.length;
    var barW = Math.max(6, Math.min(14, group * 0.28));
    var svg = '<svg viewBox="0 0 ' + w + " " + h + '" preserveAspectRatio="none" role="img" aria-label="جریان نقدی">';
    points.forEach(function (p, i) {
      var cx = pad.l + i * group + group / 2;
      var inH = ((Number(p.incoming) || 0) / max) * (h - pad.t - pad.b);
      var outH = ((Number(p.outgoing) || 0) / max) * (h - pad.t - pad.b);
      svg += '<rect x="' + (cx - barW - 2) + '" y="' + (h - pad.b - inH) + '" width="' + barW + '" height="' + inH + '" rx="4" fill="#10B981"><title>' +
        esc(p.label) + " — ورودی " + fa(Math.round(Number(p.incoming) || 0)) + "</title></rect>";
      svg += '<rect x="' + (cx + 2) + '" y="' + (h - pad.b - outH) + '" width="' + barW + '" height="' + outH + '" rx="4" fill="#EF4444"><title>' +
        esc(p.label) + " — خروجی " + fa(Math.round(Number(p.outgoing) || 0)) + "</title></rect>";
      svg += '<text x="' + cx + '" y="' + (h - 10) + '" text-anchor="middle" font-size="11" fill="#94A3B8">' + esc(p.label) + "</text>";
    });
    el.innerHTML = svg + "</svg>";
  }

  function donutChart(el, slices, center) {
    if (!slices || !slices.length) {
      el.innerHTML = empty("اطلاعاتی برای نمایش وجود ندارد.");
      return;
    }
    var total = 0;
    slices.forEach(function (s) { total += Number(s.value) || 0; });
    if (total <= 0) {
      el.innerHTML = empty("اطلاعاتی برای نمایش وجود ندارد.");
      return;
    }
    var colors = ["#2563EB", "#10B981", "#F59E0B", "#8B5CF6", "#94A3B8"];
    var r = 72, cx = 90, cy = 90, circ = 2 * Math.PI * r;
    var offset = 0;
    var rings = "";
    slices.forEach(function (s, i) {
      var len = ((Number(s.value) || 0) / total) * circ;
      rings += '<circle cx="' + cx + '" cy="' + cy + '" r="' + r + '" fill="none" stroke="' + colors[i % colors.length] +
        '" stroke-width="22" stroke-dasharray="' + len + " " + (circ - len) + '" stroke-dashoffset="' + (-offset) +
        '" transform="rotate(-90 ' + cx + " " + cy + ')"/>';
      offset += len;
    });
    var legend = slices.map(function (s, i) {
      return '<div><i style="display:inline-block;width:8px;height:8px;border-radius:50%;background:' +
        colors[i % colors.length] + ';margin-left:6px"></i><span>' + esc(s.label) + " · " + fa(s.display || s.value) + "</span></div>";
    }).join("");
    el.innerHTML = '<div class="donut-wrap"><div style="position:relative;width:180px;height:180px;margin:0 auto">' +
      '<svg width="180" height="180" viewBox="0 0 180 180">' + rings + "</svg>" +
      '<div class="donut-center"><strong>' + fa(center || total) + "</strong><span>افغانی</span></div></div>" +
      '<div class="donut-legend">' + legend + "</div></div>";
  }

  function empty(text) {
    return '<div class="empty"><p>' + esc(text) + "</p></div>";
  }

  function renderAnalytics(p) {
    var buttons = document.querySelectorAll("#periods button");
    for (var i = 0; i < buttons.length; i++) {
      buttons[i].setAttribute("aria-pressed", buttons[i].getAttribute("data-period") === state.period ? "true" : "false");
    }
    requestAnimationFrame(function () {
      lineChart($("lineChart"), p.revenueExpense, state.period);
      barChart($("barChart"), sliceSeries(p.cashFlow, state.period));
    });
  }

  function renderQuick(actions) {
    var list = (actions || []).filter(function (a) { return a && a.enabled !== false; });
    var host = $("quickActions");
    if (!list.length) {
      host.innerHTML = empty("دکمه‌ای فعال نیست. از تنظیمات میانبرها را فعال کنید.");
      return;
    }
    host.innerHTML = list.map(function (a) {
      var color = a.color || "#2563EB";
      return '<button class="quick-btn" type="button" data-action="' + esc(a.dest) + '" style="background:' + esc(color) + '">' +
        '<span class="quick-ico">' + (ICONS[a.icon] || ICONS.journal) + "</span>" +
        '<span class="quick-copy"><strong>' + esc(a.title) + "</strong><em>" + esc(a.shortcut || "") + "</em></span></button>";
    }).join("");
  }

  function renderActivities(items) {
    var host = $("activities");
    if (!host) return;
    if (!items || !items.length) {
      host.innerHTML = '<div class="empty" style="min-height:64px"><p>فعالیت اخیری ثبت نشده است.</p></div>';
      return;
    }
    host.innerHTML = items.map(function (a) {
      return '<div class="activity-item"><strong>' + esc(a.title) + "</strong><p>" +
        esc(a.detail || "") + (a.time ? " · " + fa(a.time) : "") + "</p></div>";
    }).join("");
  }

  function renderAlerts(items) {
    if (!items || !items.length) {
      $("alerts").innerHTML = '<div class="empty" style="min-height:64px"><p>هشدار فعالی وجود ندارد.</p></div>';
      $("notifyDot").hidden = true;
      return;
    }
    $("notifyDot").hidden = false;
    $("alerts").innerHTML = items.map(function (a) {
      return '<div class="alert-item"><div class="alert-icon ' + esc(a.tone || "info") + '">' +
        (a.tone === "danger" ? "!" : a.tone === "warn" ? "!" : "i") + "</div><div><strong>" +
        esc(a.title) + "</strong><p>" + esc(a.detail) + "</p></div></div>";
    }).join("");
  }

  function statusBadge(s) {
    if (s === "تأیید شده" || s === "Posted" || s === "Approved") return '<span class="badge ok">تأیید شده</span>';
    if (s === "باطل شده" || s === "Reversed") return '<span class="badge off">باطل شده</span>';
    return '<span class="badge wait">در انتظار</span>';
  }

  function renderDocs(rows) {
    if (!rows || !rows.length) {
      $("docs").innerHTML = '<div class="empty"><h3>هنوز سند مالی ثبت نشده است</h3><p>برای شروع اولین سند مالی خود را ثبت کنید.</p><button class="btn btn-primary" data-action="journal" type="button">ثبت سند مالی</button></div>';
      return;
    }
    $("docs").innerHTML = "<table><thead><tr><th>شماره</th><th>تاریخ</th><th>شرح</th><th>مبلغ</th><th>وضعیت</th></tr></thead><tbody>" +
      rows.map(function (r) {
        return "<tr><td>" + fa(r.number) + "</td><td>" + fa(r.date) + "</td><td>" + esc(r.title) +
          "</td><td class=\"amount\">" + fa(r.amount) + "</td><td>" + statusBadge(r.status) + "</td></tr>";
      }).join("") + "</tbody></table>";
  }

  function renderCommandPalette(query) {
    var pages = [
      { g: "تراکنش‌های مالی", t: "ثبت سند", a: "journal" },
      { g: "تراکنش‌های مالی", t: "فروش", a: "sale" },
      { g: "تراکنش‌های مالی", t: "خرید", a: "purchase" },
      { g: "تراکنش‌های مالی", t: "دریافت وجه", a: "receive" },
      { g: "تراکنش‌های مالی", t: "پرداخت وجه", a: "pay" },
      { g: "دفترها", t: "دفتر کل", a: "ledger" },
      { g: "دفترها", t: "دفتر روزنامه", a: "daybook" },
      { g: "حساب‌ها", t: "مشتریان", a: "parties" },
      { g: "حساب‌ها", t: "صندوق‌ها", a: "funds" },
      { g: "گزارشات مالی", t: "تراز آزمایشی", a: "trial" },
      { g: "گزارشات مالی", t: "سود و زیان", a: "pnl" },
      { g: "دوره مالی", t: "افتتاح دوره مالی", a: "open-period" },
      { g: "صفحات", t: "داشبورد", a: "home" },
      { g: "صفحات", t: "انبارداری", a: "inventory" },
      { g: "اسناد", t: "صدور فاکتور", a: "invoice" }
    ];
    var q = (query || "").trim();
    var grouped = {};
    pages.forEach(function (p) {
      if (q && p.t.indexOf(q) < 0) return;
      grouped[p.g] = grouped[p.g] || [];
      grouped[p.g].push(p);
    });
    var html = "";
    Object.keys(grouped).forEach(function (g) {
      html += '<div class="search-group"><h4>' + g + "</h4>" + grouped[g].map(function (p) {
        return '<button type="button" data-action="' + p.a + '">' + p.t + "</button>";
      }).join("") + "</div>";
    });
    $("commandResults").innerHTML = html || '<div class="empty" style="min-height:80px"><p>موردی یافت نشد.</p></div>';
  }

  function openSearch() {
    $("searchOverlay").hidden = false;
    $("searchOverlay").classList.add("open");
    $("commandInput").value = $("globalSearch").value || "";
    renderCommandPalette($("commandInput").value);
    $("commandInput").focus();
  }

  function closeSearch() {
    $("searchOverlay").hidden = true;
    $("searchOverlay").classList.remove("open");
  }

  function bind() {
    document.body.addEventListener("click", function (e) {
      var btn = e.target.closest("[data-action]");
      if (btn) {
        post(btn.getAttribute("data-action"));
        closeSearch();
        $("profileMenu").classList.remove("open");
      }
    });
    $("btnTheme").onclick = function () {
      var next = state.theme === "dark" ? "light" : "dark";
      state.theme = next;
      applyChrome();
      post("theme:" + next);
      if (state.payload) renderAnalytics(state.payload);
    };
    $("btnCalendar").onclick = function () { toast($("welcomeDate").textContent || $("bannerHint").textContent); };
    $("periods").addEventListener("click", function (e) {
      var btn = e.target.closest("button");
      if (!btn) return;
      state.period = btn.getAttribute("data-period");
      if (state.payload) renderAnalytics(state.payload);
    });
    $("btnNotify").onclick = function () {
      document.querySelector(".alerts").scrollIntoView({ behavior: "smooth", block: "start" });
    };
    $("btnProfile").onclick = function (e) {
      e.stopPropagation();
      $("profileMenu").classList.toggle("open");
      $("btnProfile").setAttribute("aria-expanded", $("profileMenu").classList.contains("open") ? "true" : "false");
    };
    document.addEventListener("click", function () { $("profileMenu").classList.remove("open"); });
    $("globalSearch").addEventListener("focus", openSearch);
    $("commandInput").addEventListener("input", function () { renderCommandPalette(this.value); });
    $("searchOverlay").addEventListener("click", function (e) {
      if (e.target === $("searchOverlay")) closeSearch();
    });
    document.addEventListener("keydown", function (e) {
      if ((e.ctrlKey || e.metaKey) && (e.key === "k" || e.key === "K")) {
        e.preventDefault();
        openSearch();
      }
      if (e.key === "Escape") closeSearch();
      if (!e.ctrlKey && !e.altKey && !e.metaKey && /^F[1-5]$/.test(e.key)) {
        var actions = (state.payload && state.payload.quickActions) || [];
        var idx = parseInt(e.key.charAt(1), 10) - 1;
        var a = actions[idx];
        if (a && a.enabled !== false && a.dest) {
          e.preventDefault();
          post(a.dest);
        }
      }
    });
    window.addEventListener("resize", function () {
      if (state.payload) renderAnalytics(state.payload);
    });
  }

  function isDark() {
    return document.documentElement.getAttribute("data-theme") === "dark";
  }

  window.renderDashboard = function (payload) {
    try {
      state.payload = payload || {};
      var p = state.payload;
      state.theme = p.resolvedTheme || p.theme || state.theme || "light";
      state.density = p.density || "compact";
      state.fontScale = p.fontScale || 100;
      applyChrome();
      $("hello").textContent = "سلام " + (p.userName || "");
      $("userName").textContent = p.userName || "";
      $("userRole").textContent = p.role || "";
      $("avatar").textContent = (p.userName || "گ").charAt(0);
      $("bannerHint").textContent = p.insight || "خلاصه وضعیت مالی امروز";
      $("welcomeDate").textContent = fa(p.today || p.updatedAt || "");
      $("systemStatus").textContent = p.systemStatus || "آماده";
      $("lastLogin").textContent = fa(p.lastLogin || "—");
      $("footerLeft").textContent = (p.footer || "نسخه ۱.۰.۰") + " · تمامی حقوق محفوظ است.";
      $("footerRight").textContent = "آخرین بروزرسانی " + fa(p.updatedAt || "") + " · وضعیت اتصال: آنلاین";
      renderKpis(p.kpis);
      renderAnalytics(p);
      renderQuick(p.quickActions);
      renderAlerts(p.alerts);
      renderDocs(p.documents);
      renderActivities(p.activities);
    } catch (err) {
      $("kpiGrid").innerHTML = '<div class="error card"><h3>مشکلی در دریافت اطلاعات رخ داد.</h3><button class="btn btn-primary" type="button" id="btnRetry">تلاش مجدد</button></div>';
      var retry = $("btnRetry");
      if (retry) retry.onclick = function () { post("home"); };
    }
  };

  applyChrome();
  bind();
  if (window.__ERP_DASHBOARD__) {
    window.renderDashboard(window.__ERP_DASHBOARD__);
  } else if (!(window.chrome && window.chrome.webview)) {
    window.renderDashboard({
      userName: "محمد طاهری",
      role: "مدیر سیستم",
      theme: "light",
      resolvedTheme: "light",
      density: "compact",
      fontScale: 100,
      summary: [
        "درآمد این ماه نسبت به ماه گذشته ۱۸٪ افزایش یافته است.",
        "موجودی کالا در وضعیت مطلوب قرار دارد.",
        "هیچ هشدار بحرانی ثبت نشده است."
      ],
      footer: "نسخه ۱.۰.۰",
      updatedAt: "۱۴۰۴/۰۶/۲۰",
      today: "۱۴۰۴/۰۶/۲۰",
      lastLogin: "۱۴۰۴/۰۶/۱۹  ۰۹:۱۵",
      systemStatus: "آماده",
      expenseTotal: "720,000",
      quickActions: [
        { enabled: true, title: "ثبت سند", icon: "journal", color: "#2563EB", dest: "journal", shortcut: "F1" },
        { enabled: true, title: "صدور فاکتور", icon: "invoice", color: "#0F766E", dest: "invoice", shortcut: "F2" },
        { enabled: true, title: "دریافت وجه", icon: "receive", color: "#D97706", dest: "receive", shortcut: "F3" },
        { enabled: true, title: "گزارش مالی", icon: "chart", color: "#7C3AED", dest: "trial", shortcut: "F4" },
        { enabled: true, title: "مشتریان", icon: "people", color: "#0891B2", dest: "crm", shortcut: "F5" }
      ],
      kpis: [
        { label: "درآمد امروز", value: "136,000", unit: "افغانی", icon: "sale", change: "—", hint: "نسبت به ماه قبل" },
        { label: "درآمد ماه", value: "1,850,000", unit: "افغانی", icon: "income", change: "↑ ۱۸٪", trend: "up", hint: "نسبت به ماه قبل" },
        { label: "هزینه ماه", value: "720,000", unit: "افغانی", icon: "expense", change: "↓ ۶٪", trend: "down", hint: "نسبت به ماه قبل" },
        { label: "موجودی بانک", value: "4,250,000", unit: "افغانی", icon: "bank", change: "—", hint: "نسبت به ماه قبل" },
        { label: "موجودی صندوق", value: "380,000", unit: "افغانی", icon: "cash", change: "—", hint: "نسبت به ماه قبل" },
        { label: "سود خالص", value: "1,130,000", unit: "افغانی", icon: "profit", change: "↑ ۱۲٪", trend: "up", hint: "نسبت به ماه قبل" }
      ],
      revenueExpense: {
        income: [{ label: "حمل", value: 900000 }, { label: "ثور", value: 1100000 }, { label: "جوزا", value: 1250000 }, { label: "سرطان", value: 1400000 }, { label: "اسد", value: 1600000 }, { label: "سنبله", value: 1850000 }],
        expense: [{ label: "حمل", value: 500000 }, { label: "ثور", value: 620000 }, { label: "جوزا", value: 580000 }, { label: "سرطان", value: 700000 }, { label: "اسد", value: 740000 }, { label: "سنبله", value: 720000 }]
      },
      cashFlow: [
        { label: "حمل", incoming: 800000, outgoing: 400000 },
        { label: "ثور", incoming: 950000, outgoing: 520000 },
        { label: "جوزا", incoming: 1100000, outgoing: 480000 },
        { label: "سرطان", incoming: 1020000, outgoing: 610000 },
        { label: "اسد", incoming: 1280000, outgoing: 700000 },
        { label: "سنبله", incoming: 1400000, outgoing: 650000 }
      ],
      expenses: [
        { label: "حقوق و دستمزد", value: 252000, display: "252,000" },
        { label: "خرید کالا", value: 180000, display: "180,000" },
        { label: "هزینه اداری", value: 108000, display: "108,000" },
        { label: "خدمات و نگهداری", value: 72000, display: "72,000" },
        { label: "سایر", value: 108000, display: "108,000" }
      ],
      alerts: [
        { tone: "danger", title: "چک سررسید شده", detail: "یک فقره چک نیاز به پیگیری دارد." },
        { tone: "warn", title: "فاکتور پرداخت نشده", detail: "۳ فاکتور در انتظار پرداخت است." },
        { tone: "info", title: "موجودی کم کالا", detail: "چند قلم کالا کمتر از حد مجاز است." }
      ],
      documents: [
        { number: "1502", date: "1404/06/19", title: "دریافت از مشتری احمدی", amount: "136,000", status: "تأیید شده" },
        { number: "1501", date: "1404/06/18", title: "پرداخت هزینه اجاره", amount: "85,000", status: "در انتظار" },
        { number: "1500", date: "1404/06/18", title: "فاکتور فروش شماره ۵۰۰۱", amount: "250,000", status: "تأیید شده" },
        { number: "1499", date: "1404/06/17", title: "خرید کالای اولیه", amount: "180,000", status: "تأیید شده" }
      ],
      activities: [
        { title: "دریافت از مشتری احمدی", detail: "سند 1502", time: "1404/06/19" },
        { title: "پرداخت هزینه اجاره", detail: "سند 1501", time: "1404/06/18" },
        { title: "فاکتور فروش شماره ۵۰۰۱", detail: "سند 1500", time: "1404/06/18" }
      ]
    });
  } else {
    $("kpiGrid").innerHTML = skeletonKpis();
  }
})();
