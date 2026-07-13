; ─────────────────────────────────────────────────────────────────────────
; اسکریپت نصاب حرفه‌ای (Inno Setup) برای سیستم مدیریت پرونده‌ها
;
; آموزش — نحوه استفاده:
;   ۱. Inno Setup (رایگان) را از https://jrsoftware.org/isinfo.php نصب کنید.
;   ۲. ابتدا پروژه را در حالت Release منتشر کنید:
;      msbuild CaseManagement.csproj /p:Configuration=Release
;   ۳. این فایل را در Inno Setup Compiler باز و Compile کنید (یا از خط فرمان:
;      iscc Installer\CaseManagement.iss).
;   ۴. خروجی در پوشه Installer\Output ساخته می‌شود.
; ─────────────────────────────────────────────────────────────────────────

#define MyAppName "سیستم مدیریت پرونده‌ها"
#define MyAppVersion "1.1.0"
#define MyAppPublisher "CaseManagement"
#define MyAppExeName "CaseManagement.exe"

[Setup]
AppId={{B8F1E1A2-6C4D-4E3A-9F2B-7A5C1D8E9F00}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\CaseManagement
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=Output
OutputBaseFilename=CaseManagement_Setup_{#MyAppVersion}
Compression=lzma
SolidCompression=yes
WizardStyle=modern
; آموزش: بدون این خط، نصاب فقط رابط انگلیسی دارد. برای رابط کامل فارسی
; باید فایل ترجمه Persian.isl را کنار این اسکریپت قرار دهید (اختیاری).
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "ساخت میانبر روی Desktop"; GroupDescription: "میانبرها:"

[Files]
; آموزش: تمام خروجی Release (exe + DLLها + فایل‌های پیکربندی + قالب Word +
; فایل rdlc گزارش) کپی می‌شود. اگر مسیر خروجی build شما فرق دارد، این خط
; را اصلاح کنید.
Source: "..\bin\Release\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb,*.xml,CaseDB.sqlite,CaseFiles\*"

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\حذف {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "اجرای {#MyAppName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; آموزش: دیتابیس و فایل‌های پرونده‌ها (عکس/اسناد) کاربر عمداً حذف نمی‌شوند
; تا Uninstall باعث از دست رفتن داده نشود. فقط فایل‌های خود برنامه پاک می‌شوند.
Type: files; Name: "{app}\*.log"
