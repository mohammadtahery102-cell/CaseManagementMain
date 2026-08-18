using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace CaseManagement.Helpers
{
    // ─────────────────────────────────────────────────────────────────────────
    // تبدیل Word (.docx) به PDF — با اولویت:
    //   ۱. اتوماسیونِ Microsoft Word (اگر روی سیستم نصب باشد)
    //   ۲. LibreOffice (اگر Word نبود)
    //   ۳. خطای روشن با راهنمای نصب (اگر هیچ‌کدام نبود)
    //
    // Word با Late-binding صدا زده می‌شود (Type.GetTypeFromProgID + dynamic)
    // تا هیچ وابستگیِ کامپایل‌زمانی به PIA/Office نیاز نباشد — روی سیستمی که
    // Word ندارد هم بدون مشکل کامپایل و اجرا می‌شود (فقط این مسیر رد می‌شود).
    //
    // این کلاس جایگزینِ منطقِ تکراریِ LibreOffice-فقط در GridReportExporter و
    // FrmCase است؛ هر دو اکنون به همین‌جا واگذار می‌کنند تا رفتار یکسان بماند.
    // ─────────────────────────────────────────────────────────────────────────
    public static class PdfConversionHelper
    {
        public static bool IsAvailable()
        {
            return IsWordAvailable() || GetLibreOfficePath() != null;
        }

        public static string ConvertDocxToPdf(string docxPath)
        {
            if (IsWordAvailable())
            {
                try
                {
                    return ConvertWithWord(docxPath);
                }
                catch
                {
                    // اگر Word شکست خورد (مثلاً قفل/مجوز)، به LibreOffice برمی‌گردیم.
                }
            }

            string libreOfficePath = GetLibreOfficePath();
            if (libreOfficePath != null)
                return ConvertWithLibreOffice(docxPath, libreOfficePath);

            throw new Exception(
                "برای ساخت PDF باید Microsoft Word یا LibreOffice نصب باشد." + Environment.NewLine +
                "فعلاً می‌توانید از خروجی Word استفاده کنید." + Environment.NewLine +
                "مسیر مورد انتظار LibreOffice:" + Environment.NewLine +
                @"C:\Program Files\LibreOffice\program\soffice.exe");
        }

        private static bool IsWordAvailable()
        {
            try
            {
                return Type.GetTypeFromProgID("Word.Application") != null;
            }
            catch
            {
                return false;
            }
        }

        // wdExportFormatPDF = 17 — عددِ ثابتِ Word، بدون نیاز به Enum از PIA.
        private const int WdExportFormatPdf = 17;

        // آموزش — دو نکته‌ی حیاتیِ COM که خطای MDA «DisconnectedContext» را
        // می‌ساختند و هر دو اینجا رفع شده‌اند:
        //
        //   ۱. Marshal.FinalReleaseComObject(doc) بعد از doc.Close() اجرا
        //      می‌شد؛ ولی Close خودِ شیءِ Document را در Word نابود می‌کند، پس
        //      آزادسازیِ صریحِ آن RCW یعنی فراخوانی روی شیئی که دیگر وجود
        //      ندارد (HRESULT 0x80010114). راه‌حل: هیچ FinalReleaseComObject
        //      صریحی صدا زده نمی‌شود؛ فقط ارجاع‌ها null و GC اجرا می‌شود.
        //
        //   ۲. Word یک سرور STA است، اما خروجی جمعی (btnBatchExport) این کد را
        //      داخل Task.Run روی تردِ استخر (MTA) اجرا می‌کند؛ ساخت/آزادسازیِ
        //      Word از MTA همان خطای قطعِ context را می‌دهد. راه‌حل: کلِ کار
        //      همیشه روی یک تردِ STA اختصاصی انجام و پیش از پایانِ همان ترد
        //      پاکسازی می‌شود.
        private static string ConvertWithWord(string docxPath)
        {
            string result = null;
            Exception failure = null;

            Thread staThread = new Thread(delegate ()
            {
                try { result = ConvertWithWordCore(docxPath); }
                catch (Exception ex) { failure = ex; }
            });

            staThread.SetApartmentState(ApartmentState.STA);
            staThread.IsBackground = true;
            staThread.Start();
            staThread.Join();

            if (failure != null)
                throw failure;

            return result;
        }

        private static string ConvertWithWordCore(string docxPath)
        {
            string fullDocxPath = Path.GetFullPath(docxPath);
            string pdfPath = Path.Combine(
                Path.GetDirectoryName(fullDocxPath),
                Path.GetFileNameWithoutExtension(fullDocxPath) + ".pdf");

            if (File.Exists(pdfPath))
                File.Delete(pdfPath);

            Type wordType = Type.GetTypeFromProgID("Word.Application");
            dynamic wordApp = Activator.CreateInstance(wordType);
            dynamic doc = null;

            try
            {
                wordApp.Visible = false;
                wordApp.DisplayAlerts = 0;

                doc = wordApp.Documents.Open(fullDocxPath, ReadOnly: true);
                doc.ExportAsFixedFormat(pdfPath, WdExportFormatPdf);

                try { doc.Close(false); } catch { }
                doc = null;

                try { wordApp.Quit(false); } catch { }
                wordApp = null;
            }
            finally
            {
                // اگر در میانه‌ی کار خطایی رخ داد، Word نباید در حافظه بماند.
                if (doc != null)
                {
                    try { doc.Close(false); } catch { }
                    doc = null;
                }

                if (wordApp != null)
                {
                    try { wordApp.Quit(false); } catch { }
                    wordApp = null;
                }

                // پاکسازیِ RCWها روی همین تردِ STA، پیش از پایانِ آن.
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            if (!File.Exists(pdfPath))
                throw new Exception("Word نتوانست PDF بسازد.");

            return pdfPath;
        }

        // ─────────────────────────────────────────────────────────────────────
        // خروجیِ PDF برای یک *دسته* از فایل‌ها — به درخواستِ کاربر: خروجیِ جمعی
        // ناپایدار بود چون ConvertDocxToPdf برای هر پرونده یک نمونه‌ی تازه‌ی
        // Word باز و بسته می‌کرد (ده‌ها بار باز/بسته‌شدنِ Word روی یک دسته‌ی
        // بزرگ = کند و مستعدِ خطا/دیالوگ‌های غیرمنتظره/فرآیندهای باقی‌مانده).
        // اینجا Word فقط *یک‌بار* برای کلِ دسته باز می‌شود و در پایان بسته
        // می‌شود — دقیقاً همان رفتاری که این کلاس برای فایلِ تکی دارد، فقط
        // تکرارشونده روی یک لیست. اگر Word نبود، به همان روالِ LibreOffice
        // (که ذاتاً هر بار یک پردازه‌ی جدید است، سبک‌تر) برای تک‌تکِ فایل‌ها
        // برمی‌گردد.
        //
        // callback: بعد از هر فایل صدا زده می‌شود (docxPath, pdfPathOrNull,
        // errorOrNull) تا فراخواننده بتواند پیشرفت/شمارنده را به‌روز کند.
        public static void ConvertManyDocxToPdf(System.Collections.Generic.IList<string> docxPaths,
            Action<string, string, Exception> onEach, Func<bool> isCancelled)
        {
            if (docxPaths == null || docxPaths.Count == 0) return;

            if (IsWordAvailable())
            {
                try
                {
                    ConvertManyWithWord(docxPaths, onEach, isCancelled);
                    return;
                }
                catch (Exception ex)
                {
                    // اگر خودِ راه‌اندازیِ دسته‌ایِ Word شکست خورد (نه یک فایلِ
                    // خاص)، به مسیرِ LibreOffice/تک‌فایلی برمی‌گردیم تا کاربر
                    // حداقل با کندیِ بیشتر خروجی بگیرد، نه با شکستِ کامل.
                    Log("ConvertManyWithWord", ex);
                }
            }

            foreach (string docx in docxPaths)
            {
                if (isCancelled != null && isCancelled()) break;
                try
                {
                    string pdf = ConvertDocxToPdf(docx);
                    onEach?.Invoke(docx, pdf, null);
                }
                catch (Exception ex)
                {
                    onEach?.Invoke(docx, null, ex);
                }
            }
        }

        private static void ConvertManyWithWord(System.Collections.Generic.IList<string> docxPaths,
            Action<string, string, Exception> onEach, Func<bool> isCancelled)
        {
            Exception setupFailure = null;

            Thread staThread = new Thread(delegate ()
            {
                dynamic wordApp = null;
                try
                {
                    Type wordType = Type.GetTypeFromProgID("Word.Application");
                    wordApp = Activator.CreateInstance(wordType);
                    wordApp.Visible = false;
                    wordApp.DisplayAlerts = 0;

                    foreach (string docxPath in docxPaths)
                    {
                        if (isCancelled != null && isCancelled()) break;

                        dynamic doc = null;
                        try
                        {
                            string fullDocxPath = Path.GetFullPath(docxPath);
                            string pdfPath = Path.Combine(
                                Path.GetDirectoryName(fullDocxPath),
                                Path.GetFileNameWithoutExtension(fullDocxPath) + ".pdf");

                            if (File.Exists(pdfPath)) File.Delete(pdfPath);

                            doc = wordApp.Documents.Open(fullDocxPath, ReadOnly: true);
                            doc.ExportAsFixedFormat(pdfPath, WdExportFormatPdf);
                            try { doc.Close(false); } catch { }
                            doc = null;

                            if (!File.Exists(pdfPath))
                                throw new Exception("Word نتوانست PDF بسازد.");

                            onEach?.Invoke(docxPath, pdfPath, null);
                        }
                        catch (Exception exOne)
                        {
                            if (doc != null) { try { doc.Close(false); } catch { } }
                            onEach?.Invoke(docxPath, null, exOne);
                        }
                    }
                }
                catch (Exception exSetup)
                {
                    setupFailure = exSetup;
                }
                finally
                {
                    if (wordApp != null)
                    {
                        try { wordApp.Quit(false); } catch { }
                        wordApp = null;
                    }
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
            });

            staThread.SetApartmentState(ApartmentState.STA);
            staThread.IsBackground = true;
            staThread.Start();
            staThread.Join();

            if (setupFailure != null) throw setupFailure;
        }

        private static void Log(string where, Exception ex)
        {
            try { Enterprise.ErrorLogger.Log(ex, "PdfConversionHelper." + where); } catch { }
        }

        private static string ConvertWithLibreOffice(string docxPath, string libreOfficePath)
        {
            string outputFolder = Path.GetDirectoryName(docxPath);
            string expectedPdf = Path.Combine(outputFolder, Path.GetFileNameWithoutExtension(docxPath) + ".pdf");
            if (File.Exists(expectedPdf)) File.Delete(expectedPdf);

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = libreOfficePath,
                Arguments = "--headless --nologo --nofirststartwizard --nolockcheck --convert-to pdf " +
                            "--outdir \"" + outputFolder + "\" \"" + docxPath + "\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            var output = new StringBuilder();
            var error = new StringBuilder();
            using (Process process = new Process())
            {
                process.StartInfo = psi;
                process.OutputDataReceived += (s, a) => { if (a.Data != null) output.AppendLine(a.Data); };
                process.ErrorDataReceived += (s, a) => { if (a.Data != null) error.AppendLine(a.Data); };
                if (!process.Start()) throw new Exception("LibreOffice اجرا نشد.");
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                if (!process.WaitForExit(180000))
                {
                    try { process.Kill(); } catch { }
                    throw new Exception("زمان ساخت PDF بیش از حد طولانی شد.");
                }
                process.WaitForExit();
                if (!File.Exists(expectedPdf))
                    throw new Exception("LibreOffice نتوانست PDF بسازد. " + output + " " + error);
            }
            return expectedPdf;
        }

        private static string GetLibreOfficePath()
        {
            string[] candidates =
            {
                @"C:\Program Files\LibreOffice\program\soffice.exe",
                @"C:\Program Files (x86)\LibreOffice\program\soffice.exe"
            };
            foreach (string p in candidates)
                if (File.Exists(p)) return p;

            try
            {
                string pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
                foreach (string dir in pathEnv.Split(Path.PathSeparator))
                {
                    if (string.IsNullOrWhiteSpace(dir)) continue;
                    string candidate = Path.Combine(dir.Trim(), "soffice.exe");
                    if (File.Exists(candidate)) return candidate;
                }
            }
            catch { }
            return null;
        }
    }
}
