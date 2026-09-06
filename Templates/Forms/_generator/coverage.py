# -*- coding: utf-8 -*-
"""مقایسهٔ توکن‌های قالب‌ها با توکن‌هایی که CaseFormTokens تولید می‌کند."""
import io, re, sys, os, zipfile

SRC = r"C:\Projects\CaseManagement\Helpers\CaseFormTokens.cs"
TPL = r"C:\Projects\CaseManagement\Templates\Forms"
NEW = ["فورم ۱ - درخواست ایتام.docx", "فورم ۲ - درخواست نیازمندان.docx",
       "فورم ۳ - درخواست درمان.docx", "فورم ۴ - تحقیق و بررسی.docx",
       "فورم ۷ - پرونده بخش درمان.docx"]

src = io.open(SRC, encoding="utf-8").read()

produced = set(re.findall(r't\["([A-Za-z0-9_]+)"\]\s*=', src))

# حلقهٔ عائله:  t[p + "Name"] = ...
for suf in re.findall(r't\[\s*p\s*\+\s*"([A-Za-z0-9_]+)"\s*\]', src):
    for n in range(1, 9):
        produced.add("F%d%s" % (n, suf))

# آرایه‌های BlankGroups — هر رشتهٔ داخلِ  string[] xxx = { ... };
for block in re.findall(r'string\[\]\s+\w+\s*=\s*\{(.*?)\};', src, re.S):
    for name in re.findall(r'"([A-Za-z0-9_]+)"', block):
        produced.add(name)

bad = 0
for name in NEW:
    with zipfile.ZipFile(os.path.join(TPL, name)) as z:
        xml = b"".join(z.read(n) for n in z.namelist()
                       if n.startswith("word/") and n.endswith(".xml"))
    text = re.sub(r"<[^>]+>", "", xml.decode("utf-8"))
    toks = set(re.findall(r"\{\{([A-Za-z0-9_]+)\}\}", text))
    missing = sorted(toks - produced)
    bad += len(missing)
    print("%-38s tokens=%-4d missing=%d" % (name[:36], len(toks), len(missing)))
    for m in missing:
        print("      -", m)

print("\nproduced:", len(produced))
sys.exit(1 if bad else 0)
