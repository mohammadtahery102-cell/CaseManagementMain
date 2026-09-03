# راهنمای قالب گزارش پرونده
## Report Template Guide — Ganjineh Case Management System

نسخه: ۳ — تاریخ: ۱۴۰۵/۰۵

> **تغییرات نسخهٔ ۳** (خواستهٔ مستقیم کاربر):
> - نام مؤسسه از سربرگ حذف شد؛ عنوان سند فقط «پرونده نیازمندان».
> - زیرعنوان تکراری «مشخصات سرپرست خانواده» از سربرگ صفحهٔ اول حذف شد.
> - همزهٔ اضافه (U+0654) که در فونت B Nazanin به شکل «دایره» چاپ می‌شد، از همهٔ
>   متن‌های قالب پاک شد («پروندهٔ» → «پرونده»، «درجهٔ» → «درجه» و …).
> - کل مشخصات سرپرست در **صفحهٔ اول** جا داده شد؛ اعضا از **صفحهٔ دوم** شروع
>   می‌شوند و هر عضو یک صفحهٔ کامل و دست‌نخورده دارد.
> - در بخش «تأیید و امضا» ستون **تهیه‌کننده حذف** شد؛ فقط «بررسی‌کننده» و
>   «مسئول مربوطه» ماند.
> - فیلدهای جاافتاده اضافه شدند: «دلیل قطع موقت» سرپرست، و برای اعضا مذهب،
>   وضعیت تأهل، وضعیت خدمات، دلیل قطع موقت و توضیح معلولیت.
> - «آدرس لوکیشن» یک **لینک کلیک‌شدنی** گرفت که موقعیت را روی نقشه باز می‌کند
>   (هم در Word و هم در PDF).
>
> **تغییرات نسخهٔ ۳٫۱:**
> - ستون عکسِ سرپرست و عکسِ عضو از راست به **چپ** منتقل شد.
> - عکس جمعی خانواده **بزرگ‌تر** شد (۲۳۰×۶۶pt) و **چپ‌چین** شد.
> - کادرهای متنیِ خالی («شرح وضعیت فوری»، «ارزیابی وضعیت خانواده») و فاصله‌های
>   اضافی کوچک‌تر شدند تا با وجود عکس بزرگ‌تر، سرپرست همچنان یک صفحه بماند.

---

## ۱. ساختار قالب

فایل: `Templates/FullCaseTemplate.docx`

یک فایل، **دو گزارش**، جداشده با Page Break:

| صفحه | گزارش | عنوان |
|---|---|---|
| ۱–۲ | گزارش سرپرست | «پروندهٔ نیازمند — مشخصات سرپرست خانواده» |
| ۳ به بعد | گزارش اعضا | «پروندهٔ نیازمند — مشخصات اعضای خانواده» |

هر دو گزارش **هویت بصری یکسان** دارند: همان سربرگ، همان رنگ‌ها، همان تایپوگرافی، همان سبک بخش‌بندی.

### مشخصات فنی

| مورد | مقدار |
|---|---|
| اندازهٔ صفحه | A4 (۲۱۰×۲۹۷ میلی‌متر) |
| حاشیه‌ها | ۱٫۲ سانتی‌متر چپ/راست · ۰٫۷ سانتی‌متر بالا/پایین |
| جهت | راست‌به‌چپ (RTL) در سطح سند، پاراگراف، متن و جدول |
| فونت فارسی | **B Nazanin** (همان فونت قالب قبلی) |
| فونت لاتین/اعداد | **Tahoma** |
| اندازهٔ متن | ۹٫۵pt بدنه · ۸٫۵pt برچسب · ۹٫۵pt عنوان بخش · ۱۵pt عنوان سند |

### رنگ‌ها

| نقش | کد |
|---|---|
| سرمه‌ای اصلی (سربرگ، نوار بخش) | `1B3A5C` |
| آبی مکمل (عنوان عضو، خط تأکید) | `2C5F8D` |
| پس‌زمینهٔ برچسب | `EAF0F6` |
| پس‌زمینهٔ ستون عکس | `F7F9FC` |
| خطوط جدول | `B8C4D0` |
| متن کم‌رنگ | `5A6B7C` |

---

## ۲. بخش‌های گزارش سرپرست

۱. **سربرگ** — نام مؤسسه، عنوان «پروندهٔ نیازمند»، زیرعنوان
۲. **نوار شناسایی** — شماره پرونده، شماره فرم، کد اختصاصی، وضعیت خدمات، اولویت، نوع درخواست
۳. **عکس سرپرست + مشخصات فردی** (کنار هم)
۴. **اسناد هویتی**
۵. **تماس و آدرس**
۶. **وضعیت اجتماعی و خدمات**
۷. **تاریخ‌ها و بازدید**
۸. **شرح وضعیت فوری** (کادر بزرگ و کشسان)
۹. **عکس جمعی خانواده**
۱۰. **تأیید و امضا** — تهیه‌کننده / بررسی‌کننده / تأییدکننده

## ۳. بخش‌های گزارش اعضا

۱. **سربرگ** (یکسان با گزارش سرپرست)
۲. **خلاصهٔ خانواده**
۳. **کارت هر عضو** — تکرارشونده، بدون محدودیت تعداد
۴. **ارزیابی وضعیت خانواده** — کادر خالی برای تکمیل دستی کارشناس
۵. **تأیید و امضا**

---

## ۴. فهرست کامل Placeholderها

### گزارش سرپرست — ۳۷ فیلد متنی + ۲ عکس

| Placeholder | ستون دیتابیس | جدول |
|---|---|---|
| `{{CasID}}` | CasID | TblCase |
| `{{FormNo}}` | FormNo | TblCase |
| `{{Code}}` | Code | TblCase |
| `{{CaseNo}}` | CaseNo | TblCase |
| `{{CaseDate}}` | CaseDate | TblCase |
| `{{Zone}}` | Zone | TblCase |
| `{{Province}}` | Province | TblCase |
| `{{District}}` | District | TblCase |
| `{{RequestType}}` | RequestType | TblCase |
| `{{PriorityLevel}}` | PriorityLevel | TblCase |
| `{{HeadFullName}}` | HeadFullName | TblCase |
| `{{HeadFatherName}}` | HeadFatherName | TblCase |
| `{{HeadSadat}}` | HeadSadat | TblCase |
| `{{Religion}}` | Religion | TblCase |
| `{{HeadIdCardType}}` | HeadIdCardType | TblCase |
| `{{HeadTazkiraNo}}` | HeadTazkiraNo | TblCase |
| `{{PhysicalStatusNotes}}` | PhysicalStatusNotes | TblCase |
| `{{HeadOriginalResidence}}` | HeadOriginalResidence | TblCase |
| `{{HeadCurrentResidence}}` | HeadCurrentResidence | TblCase |
| `{{RelationshipToFamily}}` | RelationshipToFamily | TblCase |
| `{{Phone}}` | Phone | TblCase |
| `{{RelativePhone}}` | RelativePhone | TblCase |
| `{{CoveredByOrg}}` | CoveredByOrg | TblCase |
| `{{Job}}` | Job | TblCase |
| `{{Skill}}` | Skill | TblCase |
| `{{DisabilityDegree}}` | DisabilityDegree | TblCase |
| `{{DisabilityType}}` | DisabilityType | TblCase |
| `{{MigrationCardType}}` | MigrationCardType | TblCase |
| `{{MaritalStatus}}` | MaritalStatus | TblCase |
| `{{Surveyors}}` | Surveyors | TblCase |
| `{{SurveyDate}}` | SurveyDate | TblCase |
| `{{LocationAddress}}` | LocationAddress | TblCase |
| `{{EducationLevel}}` | EducationLevel | TblCase |
| `{{ServiceStatus}}` | ServiceStatus | TblCase |
| `{{UrgentSituation}}` | UrgentSituation | TblCase |
| `{{StopReason}}` | StopReason | TblCase |
| `{{LocationLink}}` | LocationAddress → لینک کلیک‌شدنی نقشه | TblCase |
| `{{FamilyCount}}` | (محاسبه‌شده) تعداد ردیف‌های TblFamily | — |
| `{{DocsCount}}` | (محاسبه‌شده) تعداد ردیف‌های TblDocs | — |
| `{{HeadPhoto}}` | PhotoPath → تصویر ۹۰×۱۱۰pt | TblCase |
| `{{FamilyPhoto}}` | FamilyPhotoPath → تصویر ۲۵۰×۱۶۰pt | TblCase |

### گزارش اعضا — ۲۸ فیلد متنی + ۱ عکس

| Placeholder | ستون دیتابیس |
|---|---|
| `{{MemberTitle}}` | (محاسبه‌شده) «عضو شماره N» |
| `{{FamID}}` | FamID |
| `{{MemberName}}` | MemberName |
| `{{MemberFatherName}}` | MemberFatherName |
| `{{MemberIdCardType}}` | MemberIdCardType |
| `{{MemberTazkiraNo}}` | MemberTazkiraNo |
| `{{BirthDate}}` | BirthDate |
| `{{MemberSadat}}` | MemberSadat |
| `{{Gender}}` | Gender |
| `{{PhysicalStatus}}` | PhysicalStatus |
| `{{HasDisability}}` | HasDisability |
| `{{MemberDisabilityDegree}}` | MemberDisabilityDegree |
| `{{MemberEducation}}` | MemberEducation |
| `{{EducationCoverage}}` | EducationCoverage |
| `{{SchoolName}}` | SchoolName |
| `{{SchoolType}}` | SchoolType |
| `{{GradeLevel}}` | GradeLevel |
| `{{SchoolPrevGrade}}` | SchoolPrevGrade |
| `{{UniversityName}}` | UniversityName |
| `{{UniversityType}}` | UniversityType |
| `{{StudyYear}}` | StudyYear |
| `{{UniversityPrevGrade}}` | UniversityPrevGrade |
| `{{Major}}` | Major |
| `{{StudyField}}` | StudyField |
| `{{SeminaryLevel}}` | SeminaryLevel |
| `{{MemberSkill}}` / `{{Skill}}` | Skill |
| `{{LeaveReason}}` / `{{OfficialStatus}}` | LeaveReason |
| `{{Details}}` | Details |
| `{{MemberReligion}}` | Religion |
| `{{MemberMaritalStatus}}` | MaritalStatus |
| `{{MemberServiceStatus}}` | ServiceStatus |
| `{{MemberStopReason}}` | StopReason |
| `{{DisabilityDetails}}` | DisabilityDetails |
| `{{MemberPhoto}}` | MemberPhotoPath → تصویر ۸۵×۱۰۵pt |

همهٔ فیلدهای عضو از جدول `TblFamily` می‌آیند.

### پوشش کامل فرم‌ها

هر فیلدی که در `FrmCase` و `FrmFamily` وجود دارد، در قالب یک جای مشخص دارد.
تنها استثناها ستون‌هایی هستند که هیچ کنترلی در فرم ندارند (`HeadBirthDate`،
`OfficialStatus`، `GlobalID`، `CenterID`، `CreatedAt/UpdatedAt`) و عمداً چاپ
نمی‌شوند.

### نشانگرهای بلوک تکرارشونده

```
{{FamilyBlockStart}}
   ... کارت عضو ...
{{FamilyBlockEnd}}
```

موتور (`OpenXmlCaseExporter.FillFamilyBlock`) **هر عنصر سطح‌بدنه** بین این دو نشانگر را برای هر عضو یک بار کلون می‌کند. تعداد اعضا محدودیتی ندارد.

---

## ۵. قواعد حیاتی هنگام ویرایش قالب

این چهار قاعده از تجربهٔ واقعی به دست آمده‌اند؛ نقض هرکدام سند را خراب می‌کند:

### ۱) هر placeholder باید **داخل یک جدول** باشد
موتور برای جای‌گزینی از `Descendants<Paragraph>()` روی هر عنصر بلوک استفاده می‌کند و `Descendants` **خودِ عنصر را شامل نمی‌شود**. اگر placeholder را در یک پاراگرافِ مستقل (نه داخل جدول) بگذارید، هرگز پر نمی‌شود و در سند چاپی خالی یا خام می‌ماند.

> `{{MemberTitle}}` به همین دلیل به‌صورت اولین سطرِ جدولِ کارت عضو قرار گرفته است.

### ۲) بین دو جدول باید یک پاراگراف باشد
Word دو جدولِ چسبیده را در هم ادغام می‌کند. اگر تعداد ستون‌هایشان فرق کند، چیدمان کاملاً به‌هم می‌ریزد.

### ۳) سند نباید با جدول تمام شود
اگر آخرین عنصرِ بدنه یک جدول باشد، Word فایل را «خراب» گزارش می‌کند. یک پاراگراف خالی در انتها الزامی است.

### ۵) صفحه‌بندی شکننده است
گزارش سرپرست دقیقاً به‌اندازهٔ یک صفحهٔ A4 تنظیم شده. ارتفاع سطرها، حاشیهٔ
صفحه (۴۰۰ twip بالا/پایین)، اندازهٔ فونت‌ها و **اندازهٔ عکس‌ها در
`OpenXmlCaseExporter`** با هم هماهنگ‌اند. بزرگ‌کردن هرکدام، دو سطر آخرِ بخش امضا
را به صفحهٔ دوم می‌اندازد و اعضا یک صفحه عقب می‌افتند.

کارت هر عضو با `keepNext` یکپارچه نگه داشته می‌شود تا بین دو صفحه نشکند؛
نتیجه: هر عضو یک صفحهٔ کامل.

### ۴) ترتیب عناصر XML اجباری است
- داخل `<w:rPr>`: `rFonts → b → bCs → color → sz → szCs → rtl`
- داخل `<w:tblPr>`: `bidiVisual → tblW → tblBorders → tblLayout → tblCellMar`
- داخل `<w:tcPr>`: `tcW → gridSpan → shd → vAlign`
- داخل `<w:tblBorders>`: `top → left → bottom → right → insideH → insideV`

### ۵) در پاراگراف RTL، `w:jc` برعکس است
در استاندارد OOXML مقدارهای `left` و `right` نام قدیمیِ `start` و `end` هستند، نه چپ و راست فیزیکی. پس در پاراگرافی که `<w:bidi/>` دارد:

| مقدار | نتیجهٔ دیده‌شده |
|---|---|
| `<w:jc w:val="left"/>` | **راست‌چین** (start) ✅ |
| `<w:jc w:val="right"/>` | **چپ‌چین** (end) ❌ |
| `<w:jc w:val="center"/>` | وسط‌چین (فقط برای سلول عکس) |

> ⚠ نسخهٔ اول قالب همه‌جا `right` داشت و به همین دلیل عنوان بخش‌ها («اسناد هویتی و شماره‌ها» و بقیه) و متن سلول‌ها به سمت چپ چسبیده بودند. همهٔ ۲۲۵ مورد به `left` تغییر کرد و اکنون کل سند راست‌چین است. هنگام افزودن سلول جدید حتماً `left` بگذارید.

---

## ۶. افزودن فیلد جدید در آینده

افزودن یک فیلد **دو گام** دارد. قالب به‌تنهایی کافی نیست.

**گام ۱ — کد:** در `Helpers/OpenXmlCaseExporter.cs`

- برای فیلد سرپرست → به `BuildHeadValues` اضافه کنید:
  ```csharp
  { "{{MyNewField}}", GetValue(row, "MyNewColumn") },
  ```
- برای فیلد عضو → به `BuildFamilyValues` اضافه کنید.
- برای تاریخ از `GetDate(row, "...")` استفاده کنید.

**گام ۲ — قالب:** `{{MyNewField}}` را در یک سلول جدول قرار دهید (قاعدهٔ ۱).

> ⚠ اگر placeholder را فقط در قالب بگذارید و در کد ثبت نکنید، روی سند چاپ‌شده عیناً `{{MyNewField}}` چاپ می‌شود.

---

## ۷. فیلدهای درخواست‌شده که فعلاً منبع داده ندارند

این موارد **عمداً** در قالب نیامده‌اند، چون هیچ ستونی در دیتابیس ندارند و گذاشتنشان باعث چاپ شدن کد خام یا کادر همیشه‌خالی می‌شد:

| فیلد | وضعیت |
|---|---|
| قومیت (Ethnicity) | ستون وجود ندارد |
| جنسیت سرپرست | فقط برای اعضا موجود است |
| شمارهٔ ثبت (Registration No.) | ستون وجود ندارد |
| قریه (Village) | ستون وجود ندارد |
| وضعیت مسکن / درآمد / اشتغال | ستون وجود ندارد |
| تاریخ تأیید / بستن پرونده | ستون وجود ندارد |
| تعداد کودکان / تعداد ایتام | محاسبه نمی‌شود |
| سن اعضا | فقط `BirthDate` هست؛ سن محاسبه نمی‌شود |
| شغل عضو / تأهل عضو / وضعیت پوشش عضو | در دیتابیس هست ولی به موتور وصل نشده |

معادل‌های واقعی که **استفاده شده‌اند**:

| درخواست | فیلد استفاده‌شده |
|---|---|
| تماس اضطراری | `{{RelativePhone}}` |
| نیازهای فوری | `{{UrgentSituation}}` |
| خدمات جاری | `{{ServiceStatus}}` |
| تاریخ بازبینی | `{{SurveyDate}}` |
| اشتغال | `{{Job}}` |
| دستهٔ حمایت | `{{RequestType}}` + `{{CoveredByOrg}}` |

برای افزودن هرکدام، بخش ۶ را دنبال کنید.

---

## ۸. تولید خروجی PDF

سیستم از زیرساخت موجود استفاده می‌کند: `Helpers/PdfConversionHelper.cs`
اولویت: **Microsoft Word** → **LibreOffice** → خطای راهنما.

### فایل‌های نمونه

| فایل | توضیح |
|---|---|
| `Templates/FullCaseTemplate.docx` | قالب واقعی — همراه برنامه نصب می‌شود (در `.csproj` ثبت است) |
| `Templates/FullCaseTemplate_Sample.docx` | خروجی نمونه با دادهٔ آزمایشی — فقط برای بازبینی، نصب نمی‌شود |
| `Templates/FullCaseTemplate_Sample.pdf` | همان نمونه پس از تبدیل با Word — ۵ صفحهٔ A4 |

نمونه‌ها با همان مسیر تولیدی ساخته می‌شوند. برای بازتولید پس از هر تغییر قالب:

```powershell
$env:REPORT_SAMPLE_OUT = "<یک پوشهٔ خالی>"
dotnet test CaseManagement.Tests --filter "FullyQualifiedName~CaseReportTemplateTests"
```

سپس `FullCaseTemplate_Sample.docx` و `FullCaseTemplate_Sample.pdf` را از آن پوشه به `Templates/` کپی کنید.
(فایل‌های `case_*.docx` در آن پوشه خروجی‌های میانی آزمون‌اند و کپی نمی‌شوند.)

برای گرفتن PDF از قالب یا از یک پروندهٔ خروجی:
```
Word → File → Export → Create PDF/XPS
```
یا با LibreOffice:
```
soffice --headless --convert-to pdf FullCaseTemplate.docx
```

---

## ۹. آزمون‌های خودکار

فایل: `CaseManagement.Tests/CaseReportTemplateTests.cs` — ۱۲ آزمون

| آزمون | چه چیزی را تضمین می‌کند |
|---|---|
| `Template_ExistsAndOpensAsValidWordDocument` | فایل سالم باز می‌شود |
| `Template_ContainsRepeatingFamilyBlockMarkers` | فیلدهای عضو داخل بلوک تکرارشونده‌اند |
| `Template_IsRightToLeft` | نشانگرهای RTL در سطح پاراگراف، متن و جدول |
| `Template_UsesA4PageSize` | ابعاد A4 |
| `Export_LeavesNoUnfilledPlaceholders` | هیچ `{{...}}` خامی چاپ نمی‌شود |
| `Export_ContainsGuardianData` | دادهٔ واقعی سرپرست چاپ می‌شود |
| `Export_RepeatsBlockForEveryFamilyMember` | تکرار دقیق به تعداد اعضا |
| `Export_WithManyMembers_DoesNotBreak` | ۲۵ عضو بدون شکستن چیدمان |
| `Export_WithNoFamilyMembers_StillProducesValidDocument` | پروندهٔ بدون عضو |
| `Export_WithEmptyOptionalFields_...` | فیلد خالی، خالی چاپ می‌شود نه کد |
| `Export_DocumentSectionsAppearInCorrectOrder` | ترتیب دو گزارش |
| `Export_ConvertsToPdfThroughExistingPipeline` | تبدیل PDF (در نبود Office «نامشخص») |

پس از هر تغییر در قالب، این آزمون‌ها را اجرا کنید:
```
dotnet test CaseManagement.Tests --filter "FullyQualifiedName~CaseReportTemplateTests"
```

---

## ۱۰. اشکال شناسایی‌شده در موتور (اصلاح نشده)

هنگام آزمون، یک اشکالِ **از قبل موجود** در `OpenXmlCaseExporter.FillFamilyBlock` پیدا شد:

در حالتِ «پرونده بدون عضو»، پیام «هیچ عضو خانواده ثبت نشده است.» درج می‌شود و بلافاصله `RemoveBlockElements` با اندیس‌های قدیمی صدا زده می‌شود؛ چون درج، اندیس‌ها را یک واحد جابه‌جا کرده، همان پیام دوباره حذف می‌شود.

**اثر:** پروندهٔ بدون عضو، هیچ توضیحی در بخش اعضا ندارد (بخش خالی می‌ماند).

**چرا اصلاح نشد:** این وظیفه صریحاً فقط «طراحی قالب» بود و تغییر منطق نرم‌افزار مجاز نبود. اصلاحش یک تغییر تک‌خطی است (درج پیام *پس از* حذف بلوک).
