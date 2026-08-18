/* AssistanceReceipt — renders receipts from sample/SAMPLE_DATA.json.
   Contract: fetch a single object (single-print) or an array (batch print).
   Field names/semantics are documented in docs/FIELD_MAPPING.md — mirrors
   the GuardianCard/guardian-card.js convention used elsewhere in this app. */
(function () {
  function esc(s) {
    return String(s == null ? '' : s).replace(/[&<>"']/g, function (c) {
      return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
    });
  }

  function field(label, value, cls) {
    if (value === undefined || value === null || value === '') return '';
    return '<div class="rc-field' + (cls ? ' ' + cls : '') + '"><span class="lbl">' + esc(label) + ':</span><span class="val">' + esc(value) + '</span></div>';
  }

  function img(src, alt, imgClass, phClass, phText) {
    if (!src) return '<div class="' + phClass + '">' + esc(phText) + '</div>';
    return '<img class="' + imgClass + '" src="' + esc(src) + '" alt="' + esc(alt) + '" onerror="this.outerHTML=\'<div class=&quot;' + phClass + '&quot;>' + esc(phText) + '<\/div>\'">';
  }

  function barcode(src, mini) {
    var cls = mini ? 'rc-barcode-mini' : 'rc-barcode';
    if (!src) return '<div class="' + cls + ' rc-barcode-ph"></div>';
    return '<img class="' + cls + '" src="' + esc(src) + '" alt="بارکد" onerror="this.className=\'' + cls + ' rc-barcode-ph\';this.removeAttribute(\'src\')">';
  }

  function watermarkSpans(n) {
    var out = '';
    for (var i = 0; i < n; i++) out += '<span>اصل</span>';
    return out;
  }

  var microText = 'اصل • غیرقابل کاپی • برگه دریافتی مساعدت • '.repeat(6);

  function renderReceipt(d) {
    return '' +
    '<div class="receipt">' +
      '<div class="rc-security-bg"></div>' +
      '<div class="rc-watermark">' + watermarkSpans(15) + '</div>' +
      '<div class="rc-micro top">' + microText + '</div>' +
      '<div class="rc-micro bottom">' + microText + '</div>' +
      '<i class="rc-corner tl"></i><i class="rc-corner tr"></i><i class="rc-corner bl"></i><i class="rc-corner br"></i>' +
      '<div class="rc-holo"></div>' +
      '<div class="rc-row">' +
        '<div class="rc-main">' +
          '<div class="rc-header">' +
            img(d.Logo, 'آرم', 'rc-logo', 'rc-logo-ph', 'آرم') +
            '<div style="display:flex;flex-direction:column;gap:1mm">' +
              '<span class="rc-org-name">' + esc(d.OrganizationName || '[نام یا آرم خیریه]') + '</span>' +
              '<span class="rc-org-tag">سیستم مدیریت خیریه</span>' +
            '</div>' +
            '<div style="flex:1"></div>' +
            '<div class="rc-code-box">' +
              '<span class="rc-code-label">کد اختصاصی</span>' +
              '<span class="rc-code-val">' + esc(d.ReceiptCode) + '</span>' +
            '</div>' +
          '</div>' +
          '<h1 class="rc-title">برگه دریافتی مساعدت</h1>' +
          '<div class="rc-hr"></div>' +
          '<div style="display:flex;gap:5mm">' +
            '<div class="rc-photo-wrap">' +
              '<i class="rc-corner small tl"></i><i class="rc-corner small tr"></i><i class="rc-corner small bl"></i><i class="rc-corner small br"></i>' +
              img(d.Photo, 'عکس دریافت‌کننده', 'rc-photo', 'rc-photo-ph', 'عکس دریافت‌کننده') +
            '</div>' +
            '<div class="rc-fields">' +
              field('نام', d.RecipientName) +
              field('نام پدر', d.FatherName) +
              field('شماره تذکره', d.TazkiraNo) +
              field('شماره تلفن', d.Phone) +
              field('نوع و مقدار کمک', d.AidTypeAndAmount, 'rc-span2') +
            '</div>' +
          '</div>' +
          '<div class="rc-hr-dashed rc-fields3">' +
            field('تاریخ توزیع', d.DistributionDate) +
            field('ولایت/ولسوالی', d.ProvinceDistrict) +
            field('نوع درخواستی', d.RequestType) +
            field('اعضای خانواده', d.FamilyMembersCount) +
            field('نام برنامه/پروژه', d.ProgramName, 'rc-span2') +
            field('محل دریافت مساعدت', d.PickupLocation, 'rc-span2') +
            field('کارت بیجاشدگان/مهاجرین', d.DisplacedCardNo) +
            field('شماره تماس هماهنگ‌کننده', d.CoordinatorPhone, 'rc-span2') +
          '</div>' +
          '<div class="rc-footer">' +
            '<div style="display:flex;flex-direction:column;align-items:center;gap:1mm">' +
              '<div class="rc-finger"></div><span style="font-size:8.5px;color:var(--muted)">اثر انگشت دریافت‌کننده</span>' +
            '</div>' +
            '<div style="flex:1;display:flex;flex-direction:column;align-items:flex-end;gap:1mm">' +
              barcode(d.Barcode, false) +
              '<span class="rc-serial">' + esc(d.SerialNo) + '</span>' +
            '</div>' +
          '</div>' +
          '<p class="rc-warning">این برگه بدون امضا/اثر انگشت معتبر و در صورت کاپی فاقد اعتبار می‌باشد.</p>' +
        '</div>' +
        '<div class="rc-stub">' +
          '<span class="rc-scissors">✂ خط جداکردن</span>' +
          '<span class="rc-stub-title">قسمت دفتر — بایگانی</span>' +
          '<div class="rc-hr"></div>' +
          '<div style="display:flex;flex-direction:column;gap:1.8mm">' +
            field('کد', d.ReceiptCode) +
            field('نام', d.RecipientName) +
            field('نوع کمک', d.AidTypeAndAmount) +
            field('تاریخ', d.DistributionDate) +
          '</div>' +
          '<div style="flex:1"></div>' +
          '<div style="display:flex;align-items:flex-end;gap:3mm">' +
            '<div style="display:flex;flex-direction:column;align-items:center;gap:.8mm">' +
              '<div class="rc-finger-mini"></div><span style="font-size:7.5px;color:var(--muted)">اثر انگشت</span>' +
            '</div>' +
            '<div style="flex:1;display:flex;flex-direction:column;gap:.8mm">' +
              barcode(d.Barcode, true) +
              '<span class="rc-serial-mini">' + esc(d.SerialNo) + '</span>' +
            '</div>' +
          '</div>' +
        '</div>' +
      '</div>' +
    '</div>';
  }

  function render(items) {
    var root = document.getElementById('root');
    var html = '';
    for (var i = 0; i < items.length; i += 2) {
      html += '<div class="sheet">' + renderReceipt(items[i]) + (items[i + 1] ? renderReceipt(items[i + 1]) : '') + '</div>';
    }
    root.innerHTML = html;
    notifyHostRendered();
  }

  // NavigationCompleted (on the WebView2/.NET host side) only means the HTML
  // document finished loading — it does NOT wait for this file's own async
  // fetch()+render() to finish, so a print/PDF call right after navigation
  // could fire on an empty #root. This posts an explicit "done" signal once
  // rendering (success or failure) has actually happened, so the host can
  // wait for it instead of guessing from navigation timing alone.
  // window.chrome.webview only exists inside a WebView2 host; the
  // CustomEvent fires regardless, for any other in-page consumer.
  function notifyHostRendered(errorMessage) {
    document.dispatchEvent(new CustomEvent('assistancereceipt:populated', { detail: { error: errorMessage || null } }));
    if (window.chrome && window.chrome.webview) {
      try { window.chrome.webview.postMessage('assistancereceipt:populated'); } catch (e) { /* no-op */ }
    }
  }

  fetch('sample/SAMPLE_DATA.json')
    .then(function (r) { return r.json(); })
    .then(function (data) { render(Array.isArray(data) ? data : [data]); })
    .catch(function (err) {
      document.getElementById('root').innerHTML = '<p style="padding:20px;font-family:sans-serif">خطا در بارگذاری داده: ' + esc(err.message) + '</p>';
      notifyHostRendered(err.message);
    });
})();
