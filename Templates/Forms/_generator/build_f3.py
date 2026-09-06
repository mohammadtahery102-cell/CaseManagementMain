# -*- coding: utf-8 -*-
"""فورم شماره ۳ — درخواست درمان"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from formkit import *   # noqa

OUT = sys.argv[1] if len(sys.argv) > 1 else "."
MAN = sys.argv[2] if len(sys.argv) > 2 else None

SIX = six_widths()
CATEGORY = [("C_Orphan", "ایتام"), ("C_Disabled", "معلولین"), ("C_Migrant", "مهاجرین"),
            ("C_Widow", "ارامل"), ("C_BadGuardian", "بدسرپرست"),
            ("C_Vulnerable", "آسیب‌پذیر"), ("C_Other", "متفرقه")]


def build():
    reset_tokens()
    doc = new_document()

    header_block(
        doc, "فورم شماره ۳ — درخواست درمان",
        right_rows=[("ولایت", "Province"), ("ولسوالی", "District")],
        left_rows=[("تاریخ پذیرش", "AcceptDate"), ("کد اختصاصی", "Code"),
                   ("شماره پرونده", "CaseNo")],
        subtitle="بخش رسیدگی به امور بیماران")

    t = table(doc, [CONTENT_W], border_color=GRID_SOFT, cell_margin=80)
    r = row(t, height=220)
    shade(r.cells[0], BAND_BG)
    vcenter(r.cells[0])
    p = para(r.cells[0], "right", space_before=2, space_after=2)
    run(p, "اولویت‌بندی: ", bold=True, size=9, color=NAVY)
    for tk, cap in (("PR1", "اول"), ("PR2", "دوم"), ("PR3", "سوم")):
        checkbox(p, tk, cap, size=8.5, gap="    ")
    run(p, "      نوع پرونده: ", bold=True, size=9, color=NAVY)
    for tk, cap in CATEGORY:
        checkbox(p, tk, cap, size=8.5, gap="   ")
    spacer(doc, 4)

    # ── اطلاعات شخصی بیمار ──────────────────────────────────────────────
    section_bar(doc, "اطلاعات شخصی بیمار")
    t = table(doc, SIX)
    kv_row(t, [("نام و تخلص", val("HeadName")),
               ("تعداد فرزند", val("Dependents")),
               ("سیادت", chks([("SD_Aam", "عام"), ("SD_Sadat", "سادات")]))])
    kv_row(t, [("نام پدر", val("HeadFather")),
               ("نسبت با اطفال", val("RelationToChildren")),
               ("مذهب", chks([("RG_Shia", "اهل تشیع"), ("RG_Sunni", "اهل تسنن")]))])
    kv_row(t, [("شماره تذکره", val("HeadTazkira")),
               ("شغل", val("Job")),
               ("تأهل", chks([("MR_Single", "مجرد"), ("MR_Married", "متأهل")]))])
    kv_row(t, [("شماره تلفن", val("HeadPhone")),
               ("تحصیلات", val("Education")),
               ("جنسیت", chks([("GN_Male", "مرد"), ("GN_Female", "زن")]))])
    kv_row(t, [("شماره اقارب", val("RelativePhone")),
               ("تحت پوشش", val("Covered")),
               ("فوریت درخواست", chks([("U_Urgent", "عاجل"), ("U_NonUrgent", "غیرعاجل")]))])
    kv_row(t, [("تاریخ تولد", val("HeadBirthDate")),
               ("مهارت خاص", val("Skill")),
               ("معرف", val("Referrer"))])
    r = row(t)
    label_cell(r.cells[0], "سکونت اصلی")
    value_cell(merge(r, 1, 5), "OriginRes")
    r = row(t)
    label_cell(r.cells[0], "سکونت فعلی")
    value_cell(merge(r, 1, 3), "CurrentRes")
    label_cell(r.cells[4], "شماره تماس معرف")
    value_cell(r.cells[5], "ReferrerPhone")
    spacer(doc, 4)

    # ── اطلاعات درمانی بیمار ────────────────────────────────────────────
    section_bar(doc, "اطلاعات درمانی بیمار",
                note="(توسط مسئول پذیرش یا مرکز درمانی تکمیل گردد)")
    L2 = 2100
    V2 = (CONTENT_W - 2 * L2) // 2
    FOUR = [L2, V2, L2, CONTENT_W - L2 - V2 - L2]
    t = table(doc, FOUR)
    for a, b in ((("نوع بیماری", "DiseaseType"), ("حساسیت دارویی و غذایی", "DrugAllergy")),
                 (("سوابق بیماری قلبی", "HeartHistory"), ("گروه خونی", "BloodGroup")),
                 (("سوابق عمل جراحی", "SurgeryHistory"), ("وضعیت فعلی", "CurrentCondition")),
                 (("داروهای در حال مصرف", "CurrentMeds"), ("نوع درمان مورد نیاز", "TreatmentNeeded"))):
        r = row(t)
        label_cell(r.cells[0], a[0]); value_cell(r.cells[1], a[1])
        label_cell(r.cells[2], b[0]); value_cell(r.cells[3], b[1])
    spacer(doc, 4)

    # ── تعهدنامه ────────────────────────────────────────────────────────
    t = table(doc, [CONTENT_W], border_color=GRID_SOFT, cell_margin=110)
    r = row(t, height=420)
    shade(r.cells[0], BAND_BG)
    p = para(r.cells[0], "both", space_before=3, space_after=2, line_exact=230)
    run(p, "اینجانب ", size=9)
    run(p, tok("HeadName"), size=9, bold=True)
    run(p, " صحت معلومات فوق را تأیید نموده و اجازه می‌دهم خدمات صحی لازم برایم "
           "انجام گردد. همچنان به دلیل وضعیت اقتصادی ضعیف، خواهان همکاری و مساعدت "
           "مرکز درمانی می‌باشم.", size=9)
    p = para(r.cells[0], "right", space_before=4, space_after=2, line_exact=230)
    run(p, "امضاء / اثر انگشت: ", bold=True, size=9, color=NAVY)
    run(p, "_" * 34, size=9, color=MUTED)
    run(p, "        تاریخ: ", bold=True, size=9, color=NAVY)
    run(p, "_" * 26, size=9, color=MUTED)
    spacer(doc, 4)

    # ── شرح درخواست ─────────────────────────────────────────────────────
    section_bar(doc, "شرح درخواست")
    t = table(doc, [CONTENT_W], border_color=GRID_SOFT, cell_margin=80)
    r = row(t, height=1250)
    value_cell(r.cells[0], "RequestDesc", size=9)
    spacer(doc, 4)

    # ── نتیجه تحقیق و بررسی کننده ───────────────────────────────────────
    section_bar(doc, "نتیجه تحقیق و بررسی کننده")
    t = table(doc, [CONTENT_W], border_color=GRID_SOFT, cell_margin=80)
    r = row(t, height=1250)
    value_cell(r.cells[0], "SurveyResult", size=9)
    spacer(doc, 4)

    closing_block(doc, "محل الصاق عکس بیمار", [
        ("محل امضاء و شصت درخواست کننده", None),
        ("امضاء و مهر مسئول پذیرش", "تکمیل کننده فورم: " + tok("FilledBy")),
    ], height=1400)
    spacer(doc, 4)

    docs_checklist(doc, "مدارک مورد نیاز:", [
        ("DK_Identity",  "کپی تذکره بیمار"),
        ("DK_Medical",   "کپی مدارک درمانی"),
        ("DK_Photo",     "عکس پرسنلی"),
        ("DK_SurveyForm", "فورم تحقیق و بررسی"),
    ])
    footer(doc, "فورم ۳ / درخواست درمان")
    return save(doc, OUT, "فورم ۳ - درخواست درمان.docx", MAN)


if __name__ == "__main__":
    print(len(build()[1]))
