using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace CaseManagement.GuardianCardIntegration
{
    // ─────────────────────────────────────────────────────────────────────────
    // لایه یکپارچه‌سازی (Infrastructure) — تنها مسئولیتش «تحویل داده به بسته
    // فریز‌شده GuardianCard بدون دست‌زدن به آن» است.
    //
    // چرا یک پوشه کاری موقت؟ چون:
    //   ۱) قانون صریح: هیچ فایلی داخل پوشه GuardianCard تغییر نمی‌کند.
    //   ۲) خودِ مستندات GuardianCard (docs/README.md بخش ۳) دقیقاً همین روش را
    //      برای تولید کارت واقعی از C# پیشنهاد داده: «sample/SAMPLE_DATA.json را
    //      بازنویسی کن» — فقط ما این بازنویسی را روی یک کپی کاری انجام می‌دهیم،
    //      نه نسخه اصلی.
    //   ۳) guardian-card.js با fetch("sample/SAMPLE_DATA.json") کار می‌کند؛ طبق
    //      مستندات همان پروژه، fetch روی file:// در مرورگرهای Chromium (پایه
    //      WebView2) مسدود است و به داده fallback داخلی برمی‌گردد. راه‌حل
    //      مستندشدهٔ خودشان: «برای WebView2 از SetVirtualHostNameToFolderMapping
    //      استفاده کن» — این باعث می‌شود صفحه از مبدأ https://<virtual-host>
    //      بارگذاری شود، نه file://، و fetch واقعی (نه fallback) اجرا شود.
    //      این کلاس فقط فایل‌ها را آماده می‌کند؛ نگاشت virtual-host در
    //      FrmGuardianCardPreview (چون به CoreWebView2 نیاز دارد) انجام می‌شود.
    // ─────────────────────────────────────────────────────────────────────────
    public class GuardianCardRenderer
    {
        // نام هاست مجازی برای WebView2 (در FrmGuardianCardPreview استفاده می‌شود)
        public const string VirtualHostName = "guardiancard.local";

        // پوشه اصلیِ فریزشده — کنار exe دیپلوی می‌شود (Content در csproj)؛
        // هرگز نوشته/تغییر داده نمی‌شود.
        private static string BundledSourceFolder
        {
            get { return Path.Combine(Application.StartupPath, "GuardianCard"); }
        }

        // پوشه کاریِ یک‌بارمصرف — هر بار رندر یک کارت، از نو ساخته می‌شود تا هیچ
        // داده‌ای از کارت قبلی باقی نماند.
        private static string WorkingFolder
        {
            get { return Path.Combine(Path.GetTempPath(), "CaseManagement_GuardianCardWork"); }
        }

        public bool IsBundledPackagePresent()
        {
            return Directory.Exists(BundledSourceFolder) && File.Exists(Path.Combine(BundledSourceFolder, "index.html"));
        }

        // کپی کامل بسته + نوشتن JSON واقعی این پرونده + کپی تصاویر اختصاصی
        // (عکس سرپرست/لوگو مؤسسه/امضا/مهر) داخل پوشه کاری + ساخت بارکد واقعی
        // (Code128) از شماره کارت. خروجی: مسیر پوشه کاری (ریشه‌ای که
        // index.html در آن است) برای نگاشت virtual-host توسط فراخوان.
        public string StageAndPopulate(
            GuardianCardData data, string guardianPhotoPath, string orgLogoPath,
            string signaturePath = null, string stampPath = null, IEnumerable<string> disabledFields = null,
            CardTemplateDesign design = null)
        {
            if (data == null) throw new ArgumentNullException("data");
            EnsureBundledPackagePresent();

            if (Directory.Exists(WorkingFolder))
                Directory.Delete(WorkingFolder, true);
            CopyDirectory(BundledSourceFolder, WorkingFolder);

            string uploadsDir = Path.Combine(WorkingFolder, "uploads");
            Directory.CreateDirectory(uploadsDir);

            data.Photo = StageImage(guardianPhotoPath, uploadsDir, "guardian_photo");
            data.Logo = StageImage(orgLogoPath, uploadsDir, "org_logo");
            data.Signature = StageImage(signaturePath, uploadsDir, "signature");
            data.Stamp = StageImage(stampPath, uploadsDir, "stamp");
            data.Barcode = StageBarcode(BarcodeValue(data), uploadsDir, "barcode");
            data.QRCode = StageQr(BarcodeValue(data), uploadsDir, "qrcode");

            string json = new JavaScriptSerializer().Serialize(data);
            File.WriteAllText(Path.Combine(WorkingFolder, "sample", "SAMPLE_DATA.json"), json, new UTF8Encoding(false));

            ApplyDisabledFieldCleanup(WorkingFolder, disabledFields);
            ApplyDesignOverrides(WorkingFolder, uploadsDir, design);

            return WorkingFolder;
        }

        // نسخه‌ی چاپ جمعی — همان بسته یک‌بار کپی می‌شود، لوگو/امضا/مهر (که
        // مؤسسه‌ای و مشترک بین همه کارت‌هاست) فقط یک‌بار Stage می‌شوند، اما هر
        // پرونده عکس و بارکد اختصاصی خودش را می‌گیرد (نام فایل با اندیس یکتا
        // می‌شود تا رونویسی نشود). خروجی JSON یک آرایه است؛ guardian-card.js
        // این حالت را با Array.isArray تشخیص می‌دهد و کارت را به تعداد آیتم‌ها
        // Clone می‌کند (نگاه کنید populateBatch در guardian-card.js).
        public string StageAndPopulateBatch(
            IList<GuardianCardData> items, Func<GuardianCardData, string> guardianPhotoPathSelector,
            string orgLogoPath, string signaturePath = null, string stampPath = null, IEnumerable<string> disabledFields = null,
            CardTemplateDesign design = null)
        {
            if (items == null) throw new ArgumentNullException("items");
            EnsureBundledPackagePresent();

            if (Directory.Exists(WorkingFolder))
                Directory.Delete(WorkingFolder, true);
            CopyDirectory(BundledSourceFolder, WorkingFolder);

            string uploadsDir = Path.Combine(WorkingFolder, "uploads");
            Directory.CreateDirectory(uploadsDir);

            string logoRel = StageImage(orgLogoPath, uploadsDir, "org_logo");
            string signatureRel = StageImage(signaturePath, uploadsDir, "signature");
            string stampRel = StageImage(stampPath, uploadsDir, "stamp");

            for (int i = 0; i < items.Count; i++)
            {
                GuardianCardData data = items[i];
                string photoPath = guardianPhotoPathSelector != null ? guardianPhotoPathSelector(data) : data.Photo;
                data.Photo = StageImage(photoPath, uploadsDir, "guardian_photo_" + i);
                data.Logo = logoRel;
                data.Signature = signatureRel;
                data.Stamp = stampRel;
                data.Barcode = StageBarcode(BarcodeValue(data), uploadsDir, "barcode_" + i);
                data.QRCode = StageQr(BarcodeValue(data), uploadsDir, "qrcode_" + i);
            }

            string json = new JavaScriptSerializer().Serialize(items);
            File.WriteAllText(Path.Combine(WorkingFolder, "sample", "SAMPLE_DATA.json"), json, new UTF8Encoding(false));

            ApplyDisabledFieldCleanup(WorkingFolder, disabledFields);
            ApplyDesignOverrides(WorkingFolder, uploadsDir, design);

            return WorkingFolder;
        }

        // آموزش — رفعِ باگِ گزارش‌شده («خروجیِ قالب‌هایی که یک فیلدِ اختیاری را
        // خاموش می‌کنند»): بستهٔ GuardianCard برای حالتِ ویرایشِ تعاملی طراحی
        // شده، نه «فیلدِ کاملاً غایب» — وقتی مقدار خالی است، فقط چیزی نوشته
        // نمی‌شود؛ برچسبِ ثابتِ HTML (مثلاً «وبسایت: ») و placeholder ویرایشی
        // (مثلاً «or browse files») همچنان روی کارتِ چاپی/PDF می‌مانند. چون
        // تغییرِ خودِ پوشهٔ GuardianCard ممنوع است (نگاه کنید آموزشِ بالای این
        // فایل)، این متد فقط داخلِ WorkingFolder (کپیِ یک‌بارمصرفِ همین رندر)
        // دو چیز اضافه می‌کند: یک فایلِ اسکریپتِ ثابت (RemoveDisabledFieldsScript)
        // که بعد از رویدادِ «guardiancard:populated» (که guardian-card.js خودش
        // بعد از هر بار populateCard شلیک می‌کند — هم برای تک‌کارت هم برای هر
        // Clone در چاپ جمعی) ردیف/جایگاهِ کاملِ هر فیلدِ خاموش را از DOM حذف
        // می‌کند، و یک تگِ <script> کوچک که فهرستِ فیلدهای خاموش را *قبل* از
        // اجرای guardian-card.js تزریق می‌کند تا listener به‌موقع ثبت شود.
        // اگر هیچ فیلدی خاموش نباشد (مثلاً قالبِ پیش‌فرض)، هیچ فایلی نوشته/
        // تغییر داده نمی‌شود — رفتارِ قبلی دقیقاً حفظ می‌شود.
        private static void ApplyDisabledFieldCleanup(string workingFolder, IEnumerable<string> disabledFields)
        {
            var fields = new List<string>();
            if (disabledFields != null)
                foreach (string f in disabledFields)
                    if (!string.IsNullOrWhiteSpace(f))
                        fields.Add(f);

            if (fields.Count == 0) return;

            string jsDir = Path.Combine(workingFolder, "js");
            Directory.CreateDirectory(jsDir);
            File.WriteAllText(Path.Combine(jsDir, "template-overrides.js"), RemoveDisabledFieldsScript, new UTF8Encoding(false));

            string indexPath = Path.Combine(workingFolder, "index.html");
            if (!File.Exists(indexPath)) return;

            string html = File.ReadAllText(indexPath);
            const string anchor = "<script src=\"js/guardian-card.js\"></script>";
            if (html.IndexOf(anchor, StringComparison.Ordinal) < 0) return;

            var fieldList = new StringBuilder();
            for (int i = 0; i < fields.Count; i++)
            {
                if (i > 0) fieldList.Append(",");
                fieldList.Append("\"").Append(fields[i].Replace("\"", "")).Append("\"");
            }

            string injected =
                "<script>window.__cardTemplateDisabledFields = [" + fieldList + "];</script>\n" +
                "<script src=\"js/template-overrides.js\"></script>\n" +
                anchor;

            html = html.Replace(anchor, injected);
            File.WriteAllText(indexPath, html, new UTF8Encoding(false));
        }

        // آموزش — رنگ/فونت/پس‌زمینهٔ هر رو/واترمارک («Card Designer»، بخش
        // طراحِ سبک). دقیقاً همان اصلِ ApplyDisabledFieldCleanup: فقط داخلِ
        // WorkingFolder (کپیِ یک‌بارمصرف) تغییر می‌دهد، هرگز خودِ GuardianCard.
        //   • رنگ/فونت: یک <style> با override رویِ همان custom propertyهای
        //     :root که خودِ guardian-card.css تعریف کرده (--primary-color/
        //     --secondary-color/--font-family) — چون این <style> بعد از
        //     لینکِ استایلِ اصلی در <head> می‌آید، طبق قاعدهٔ cascade برنده
        //     می‌شود، بدونِ نیاز به !important.
        //   • پس‌زمینه: background-image رویِ .card-front/.card-back. آموزش —
        //     محدودیتِ صادقانه: چون پنل‌های داخلی (.panel) پس‌زمینهٔ مات دارند،
        //     این تصویر فقط در حاشیه‌ها/شکاف‌ها دیده می‌شود، نه زیرِ کل کارت
        //     (طرحِ کامل drag-and-drop نیست — دقیقاً طبقِ توافق).
        //   • واترمارک: یک <img> کم‌شفافیت که با اسکریپت (بعد از
        //     guardiancard:populated، مثلِ بقیه‌ی افزونه‌ها) به هر .card-trim
        //     اضافه می‌شود.
        private static void ApplyDesignOverrides(string workingFolder, string uploadsDir, CardTemplateDesign design)
        {
            if (design == null) return;

            bool hasColor = !string.IsNullOrWhiteSpace(design.PrimaryColor) || !string.IsNullOrWhiteSpace(design.SecondaryColor);
            bool hasFont = !string.IsNullOrWhiteSpace(design.FontFamily);
            string bgFrontRel = StageImage(design.BackgroundFrontPath, uploadsDir, "bg_front");
            string bgBackRel = StageImage(design.BackgroundBackPath, uploadsDir, "bg_back");
            string watermarkRel = StageImage(design.WatermarkPath, uploadsDir, "watermark");

            string indexPath = Path.Combine(workingFolder, "index.html");
            if (!File.Exists(indexPath)) return;
            string html = File.ReadAllText(indexPath);

            // ─── ۱) رنگ/فونت/پس‌زمینه: <style> قبل از </head> ────────────────
            if (hasColor || hasFont || bgFrontRel.Length > 0 || bgBackRel.Length > 0)
            {
                var css = new StringBuilder();
                css.Append("<style>\n:root{\n");
                if (!string.IsNullOrWhiteSpace(design.PrimaryColor))
                    css.Append("  --primary-color: ").Append(design.PrimaryColor).Append(";\n")
                       .Append("  --primary-color-dark: ").Append(design.PrimaryColor).Append(";\n");
                if (!string.IsNullOrWhiteSpace(design.SecondaryColor))
                    css.Append("  --secondary-color: ").Append(design.SecondaryColor).Append(";\n")
                       .Append("  --accent-color: ").Append(design.SecondaryColor).Append(";\n");
                if (hasFont)
                    css.Append("  --font-family: \"").Append(design.FontFamily).Append("\", Tahoma, \"Segoe UI\", Arial, sans-serif;\n");
                css.Append("}\n");
                if (bgFrontRel.Length > 0)
                    css.Append(".card-front{background-image:url('").Append(bgFrontRel).Append("');background-size:cover;background-position:center;}\n");
                if (bgBackRel.Length > 0)
                    css.Append(".card-back{background-image:url('").Append(bgBackRel).Append("');background-size:cover;background-position:center;}\n");
                css.Append("</style>\n</head>");

                const string headAnchor = "</head>";
                if (html.IndexOf(headAnchor, StringComparison.Ordinal) >= 0)
                    html = html.Replace(headAnchor, css.ToString());
            }

            // ─── ۲) واترمارک: اسکریپتِ افزودنِ <img> بعد از populated ────────
            if (watermarkRel.Length > 0)
            {
                string jsDir = Path.Combine(workingFolder, "js");
                Directory.CreateDirectory(jsDir);
                double opacity = Math.Max(0, Math.Min(100, design.WatermarkOpacityPercent)) / 100.0;
                string watermarkScript = BuildWatermarkScript(watermarkRel, opacity);
                File.WriteAllText(Path.Combine(jsDir, "watermark-overrides.js"), watermarkScript, new UTF8Encoding(false));

                const string scriptAnchor = "<script src=\"js/guardian-card.js\"></script>";
                if (html.IndexOf(scriptAnchor, StringComparison.Ordinal) >= 0)
                {
                    string injected = "<script src=\"js/watermark-overrides.js\"></script>\n" + scriptAnchor;
                    html = html.Replace(scriptAnchor, injected);
                }
            }

            File.WriteAllText(indexPath, html, new UTF8Encoding(false));
        }

        private static string BuildWatermarkScript(string watermarkRelPath, double opacity)
        {
            return @"(function () {
  ""use strict"";
  function addWatermark(trim) {
    if (!trim || trim.querySelector("".__watermark"")) return;
    var img = document.createElement(""img"");
    img.src = """ + watermarkRelPath.Replace("\"", "") + @""";
    img.className = ""__watermark"";
    img.style.position = ""absolute"";
    img.style.inset = ""0"";
    img.style.width = ""100%"";
    img.style.height = ""100%"";
    img.style.objectFit = ""contain"";
    img.style.opacity = """ + opacity.ToString(System.Globalization.CultureInfo.InvariantCulture) + @""";
    img.style.pointerEvents = ""none"";
    img.style.zIndex = ""1"";
    trim.style.position = trim.style.position || ""relative"";
    trim.appendChild(img);
  }
  function apply() {
    var trims = document.querySelectorAll("".card-trim"");
    for (var i = 0; i < trims.length; i++) addWatermark(trims[i]);
  }
  document.addEventListener(""guardiancard:populated"", apply);
})();
";
        }

        // آموزش — هر بخش دقیقاً نظیرِ یک فیلدِ Toggleable در
        // CardTemplateRepository.ToggleableFields است. چون چاپِ جمعی هر
        // کارت را با cloneNode تکثیر می‌کند (نگاه کنید populateBatch در
        // guardian-card.js) و رویدادِ «guardiancard:populated» به‌ازای هر
        // Clone دوباره روی document شلیک می‌شود، cleanup() با querySelectorAll
        // (نه querySelector) روی کلِ document اجرا می‌شود تا همهٔ Cloneها را
        // بگیرد؛ idempotent است (اجرای دوباره روی چیزی که قبلاً مخفی شده
        // بی‌ضرر است)، پس شلیکِ تکراریِ رویداد در چاپ جمعی مشکلی ایجاد نمی‌کند.
        private const string RemoveDisabledFieldsScript = @"(function () {
  ""use strict"";
  var disabled = window.__cardTemplateDisabledFields;
  if (!disabled || !disabled.length) return;
  var disabledSet = {};
  for (var i = 0; i < disabled.length; i++) disabledSet[disabled[i]] = true;

  function hide(el) { if (el) el.style.display = ""none""; }
  function isHidden(el) { return !el || el.style.display === ""none""; }

  function collapseIfEmpty(container) {
    if (!container) return;
    var children = container.children;
    for (var i = 0; i < children.length; i++) {
      if (!isHidden(children[i])) return;
    }
    hide(container);
  }

  function hideDividerBefore(el) {
    if (!el) return;
    var prev = el.previousElementSibling;
    if (prev && prev.classList.contains(""divider"")) hide(prev);
  }

  function removeSimpleRow(field, rowSelector) {
    var values = document.querySelectorAll('[data-field=""' + field + '""]');
    for (var i = 0; i < values.length; i++) {
      var row = values[i].closest(rowSelector);
      hide(row || values[i]);
    }
  }

  function removeIssuerPart(field) {
    var values = document.querySelectorAll('[data-field=""' + field + '""]');
    for (var i = 0; i < values.length; i++) {
      var part = values[i].parentElement;
      if (!part) continue;
      hide(part);
      var dot = part.nextElementSibling;
      if (dot && dot.classList.contains(""dot"")) hide(dot);
      else {
        dot = part.previousElementSibling;
        if (dot && dot.classList.contains(""dot"")) hide(dot);
      }
      collapseIfEmpty(part.parentElement);
    }
  }

  function removeImageSlot(field, slotSelector) {
    var imgs = document.querySelectorAll('img[data-field=""' + field + '""]');
    var slots = [];
    for (var i = 0; i < imgs.length; i++) {
      var slot = imgs[i].closest(slotSelector);
      hide(slot || imgs[i]);
      slots.push(slot);
    }
    return slots;
  }

  function cleanup() {
    if (disabledSet.PublicCode) removeSimpleRow(""PublicCode"", "".field-row"");
    if (disabledSet.Website) removeSimpleRow(""Website"", "".contact-row"");
    if (disabledSet.Email) removeSimpleRow(""Email"", "".contact-row"");
    if (disabledSet.IssuedBy) removeIssuerPart(""IssuedBy"");
    if (disabledSet.Position) removeIssuerPart(""Position"");
    if (disabledSet.Logo) removeImageSlot(""Logo"", "".emblem"");

    if (disabledSet.Signature) {
      var sigSlots = removeImageSlot(""Signature"", "".upload-slot"");
      for (var i = 0; i < sigSlots.length; i++) {
        var cell = sigSlots[i] ? sigSlots[i].closest("".cell"") : null;
        if (!cell) continue;
        hide(cell.querySelector("".signature-caption""));
        collapseIfEmpty(cell);
        if (isHidden(cell)) hideDividerBefore(cell);
      }
    }

    if (disabledSet.Stamp) {
      var stampImgs = document.querySelectorAll('img[data-field=""Stamp""]');
      for (var i = 0; i < stampImgs.length; i++) {
        var stampCell = stampImgs[i].closest("".cell"");
        hide(stampCell);
        hideDividerBefore(stampCell);
      }
    }

    // آموزش — QRCode/Barcode همیشه Stage می‌شوند (نگاه کنید StageQr/
    // StageBarcode)؛ اگر قالب نخواهدشان، فقط سلولِ نمایششان مخفی می‌شود —
    // دقیقاً همان الگویِ Stamp بالا.
    if (disabledSet.QRCode) {
      var qrImgs = document.querySelectorAll('img[data-field=""QRCode""]');
      for (var i = 0; i < qrImgs.length; i++) {
        var qrCell = qrImgs[i].closest("".cell"");
        hide(qrCell);
        hideDividerBefore(qrCell);
      }
    }

    if (disabledSet.Barcode) {
      var barcodeImgs = document.querySelectorAll('img[data-field=""Barcode""]');
      for (var i = 0; i < barcodeImgs.length; i++) {
        var barcodeCell = barcodeImgs[i].closest("".cell"");
        hide(barcodeCell);
        hideDividerBefore(barcodeCell);
      }
    }

    // آموزش — هولوگرام از قبل یک سوییچِ آماده در خودِ GuardianCard دارد:
    // body.show-security کارتِ گرافیکیِ واقعی را نشان می‌دهد، نبودش دایرهٔ
    // خطچین (.hologram-off) را. guardian-card.js خودش این کلاس را روی
    // body اضافه می‌کند (رفتارِ پیش‌فرض = روشن)؛ اینجا فقط اگر قالب
    // هولوگرام را نخواهد، همان کلاس را برمی‌داریم.
    if (disabledSet.Hologram) document.body.classList.remove(""show-security"");
  }

  document.addEventListener(""guardiancard:populated"", cleanup);
})();
";

        private void EnsureBundledPackagePresent()
        {
            if (!IsBundledPackagePresent())
                throw new DirectoryNotFoundException(
                    "پوشه GuardianCard کنار برنامه پیدا نشد: " + BundledSourceFolder +
                    "\nاین پوشه باید همراه نصب برنامه دیپلوی شده باشد.");
        }

        // مقدارِ بارکدِ هر خانواده — آموزش (رفعِ باگِ «همه کارت‌ها یک بارکد»):
        // قبلاً وقتی CardNumber خالی بود، به PublicCode (ستون Code، متنِ آزادِ
        // کاربر که معمولاً فارسی است) برمی‌گشت؛ Code128Barcode هر کاراکترِ
        // غیرِ ASCII را بی‌صدا حذف می‌کند و اگر چیزی نمی‌ماند مقدار را «0»
        // می‌گذارد — پس چند پرونده‌ی بدون CardNumber همه بارکدِ یکسانِ «0»
        // می‌گرفتند. اکنون:
        //   ۱) اگر CardNumber هست، دقیقاً همان مقداری که روی کارت چاپ می‌شود
        //      (BranchCode-CardNumber) کد می‌شود — این‌طور بارکد بین مراکزِ
        //      مختلف هم یکتا می‌ماند (قبلاً فقط CardNumber کد می‌شد و دو مرکز
        //      با شماره فرم یکسان بارکدِ یکسان می‌گرفتند).
        //   ۲) وگرنه از CasID (کلید یکتای واقعیِ دیتابیس، همیشه ASCII و همیشه
        //      یکتا) استفاده می‌شود — نه از PublicCode که ممکن است فارسی باشد.
        private static string BarcodeValue(GuardianCardData data)
        {
            if (data == null) return "";
            if (!string.IsNullOrWhiteSpace(data.CardNumber))
            {
                return !string.IsNullOrWhiteSpace(data.BranchCode)
                    ? data.BranchCode + "-" + data.CardNumber
                    : data.CardNumber;
            }
            return "C" + data.CasID;
        }

        // بارکد Code128 واقعی از مقدار بالا می‌سازد (هر پرونده بارکد خودش را
        // دارد، چون آن مقدار یکتاست)؛ اگر هر دو خالی باشند، بارکدی
        // ساخته نمی‌شود (placeholder خالیِ guardian-card.js جایش می‌ماند).
        private static string StageBarcode(string cardNumber, string uploadsDir, string destBaseName)
        {
            if (string.IsNullOrWhiteSpace(cardNumber))
                return "";

            string destName = destBaseName + ".png";
            Code128Barcode.SaveToFile(cardNumber, Path.Combine(uploadsDir, destName));
            return "uploads/" + destName;
        }

        // آموزش — QR واقعی از همان مقدارِ بارکد (BarcodeValue) ساخته می‌شود
        // تا اسکنِ هرکدام همان شناسهٔ کارت را بدهد. همیشه Stage می‌شود (مثلِ
        // بارکد)؛ اگر قالب QR را نخواهد، caller «QRCode» را به disabledFields
        // اضافه می‌کند تا فقط از DOM مخفی شود (نگاه کنید ApplyDisabledFieldCleanup) —
        // نه اینکه اینجا تولید نشود، تا رفتار هردو (بارکد/QR) یکسان بماند.
        private static string StageQr(string content, string uploadsDir, string destBaseName)
        {
            if (string.IsNullOrWhiteSpace(content))
                return "";

            string destName = destBaseName + ".png";
            QrCodeHelper.SaveToFile(content, Path.Combine(uploadsDir, destName));
            return "uploads/" + destName;
        }

        // کپی یک تصویر منبع (اگر موجود باشد) به uploads/ داخل پوشه کاری و
        // برگرداندن مسیر نسبیِ مناسب برای JSON؛ اگر منبع نبود، رشته خالی
        // برمی‌گردد (guardian-card.js خودش placeholder را برای مقدار خالی نگه می‌دارد).
        private static string StageImage(string sourcePath, string uploadsDir, string destBaseName)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
                return "";

            string destName = destBaseName + Path.GetExtension(sourcePath);
            File.Copy(sourcePath, Path.Combine(uploadsDir, destName), true);
            return "uploads/" + destName;
        }

        private static void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);
            foreach (string file in Directory.GetFiles(sourceDir))
                File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), true);
            foreach (string subDir in Directory.GetDirectories(sourceDir))
                CopyDirectory(subDir, Path.Combine(destDir, Path.GetFileName(subDir)));
        }
    }
}
