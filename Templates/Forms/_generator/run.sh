#!/bin/sh
# ساخت → پرکردن با دادهٔ نمونه → PDF → تصویر
SP="C:/Users/Mohammad/AppData/Local/Temp/claude/C--Projects/cc9f7eba-9b97-4333-83bd-4c4aa0dfc720/scratchpad"
cd "$SP/gen" || exit 1
rm -f "$SP/manifest.txt"
rm -f "$SP/out"/* "$SP/filled"/* "$SP/preview"/*.png 2>/dev/null
mkdir -p "$SP/out" "$SP/filled" "$SP/preview"
for f in "$@"; do
  PYTHONIOENCODING=utf-8 python "build_$f.py" "$SP/out" "$SP/manifest.txt" || exit 1
done
PYTHONIOENCODING=utf-8 python fill.py "$SP/out" "$SP/filled" >/dev/null || exit 1
powershell -NoProfile -ExecutionPolicy Bypass -File "$SP/gen/topdf.ps1" -Dir "$SP/filled" >/dev/null
PYTHONIOENCODING=utf-8 python - <<'PY'
import pymupdf, glob, os, re
SP = r"C:/Users/Mohammad/AppData/Local/Temp/claude/C--Projects/cc9f7eba-9b97-4333-83bd-4c4aa0dfc720/scratchpad"
for f in sorted(glob.glob(SP + "/filled/*.pdf")):
    d = pymupdf.open(f)
    base = os.path.basename(f)[:-4]
    key = re.sub(r"[^0-9\u06f0-\u06f9]", "", base.split("-")[0]) or "x"
    key = key.translate(str.maketrans("۰۱۲۳۴۵۶۷۸۹", "0123456789"))
    for i, p in enumerate(d):
        p.get_pixmap(dpi=125).save(SP + "/preview/f%s_p%d.png" % (key, i + 1))
    print(base.encode("ascii", "ignore").decode() or key, "pages=", d.page_count)
PY
