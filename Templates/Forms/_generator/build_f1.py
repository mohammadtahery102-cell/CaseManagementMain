# -*- coding: utf-8 -*-
"""فورم شمارهٔ ۱ — درخواست ایتام"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from formkit import *   # noqa

OUT = sys.argv[1] if len(sys.argv) > 1 else "."
MAN = sys.argv[2] if len(sys.argv) > 2 else None

SIX = six_widths()


def build():
    reset_tokens()
    doc = new_document()

    header_block(
        doc, "فورم شمارهٔ ۱ — درخواست ایتام",
        right_rows=[("ولایت", "Province"), ("ولسوالی", "District")],
        left_rows=[("تاریخ پذیرش", "AcceptDate"), ("کد اختصاصی", "Code"),
                   ("شماره پرونده", "CaseNo")],
        subtitle="بخش رسیدگی به امور ایتام")

    # ── نوارِ اولویت ─────────────────────────────────────────────────────
    t = table(doc, [CONTENT_W], border_color=GRID_SOFT, cell_margin=80)
    r = row(t, height=220)
    shade(r.cells[0], BAND_BG)
    vcenter(r.cells[0])
    p = para(r.cells[0], "right", space_before=2, space_after=2)
    run(p, "اولویت‌بندی: ", bold=True, size=9.5, color=NAVY)
    for tk, cap in (("PR1", "اول"), ("PR2", "دوم"), ("PR3", "سوم")):
        checkbox(p, tk, cap, size=9, gap="     ")
    run(p, "        نوع درخواست: ", bold=True, size=9.5, color=NAVY)
    run(p, tok("RequestTypeName"), size=9)
    spacer(doc, 5)

    # ── مشخصات سرپرست ───────────────────────────────────────────────────
    section_bar(doc, "مشخصات سرپرست")
    t = table(doc, SIX)
    kv_row(t, [("نام و تخلص", val("HeadName")),
               ("افراد تحت تکفل", val("Dependents")),
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
               ("وضعیت جسمی", chks([("PH_Healthy", "سالم"), ("PH_Disabled", "معلول")]))])
    kv_row(t, [("سکونت اصلی", val("OriginRes")),
               ("مهارت خاص", val("Skill")),
               ("شرح وضعیت جسمی", val("PhysicalNotes", size=9))])
    # سکونت فعلی (مقدار عریض) + نوع سند هویتی
    r = row(t, height=1)
    label_cell(r.cells[0], "سکونت فعلی")
    m = merge(r, 1, 3)
    value_cell(m, "CurrentRes")
    label_cell(r.cells[4], "نوع سند هویتی")
    value_cell(r.cells[5], "HeadIdCardType")
    # تاریخ تولد + لینک موقعیت
    r = row(t, height=1)
    label_cell(r.cells[0], "تاریخ تولد")
    value_cell(r.cells[1], "HeadBirthDate")
    label_cell(r.cells[2], "لینک موقعیت (GPS)")
    m = merge(r, 3, 5)
    value_cell(m, "LocationLink", size=9)
    spacer(doc, 5)

    # ── مشخصات پدر ایتام ────────────────────────────────────────────────
    section_bar(doc, "مشخصات پدر ایتام (متوفی)")
    t = table(doc, SIX)
    kv_row(t, [("نام و تخلص", val("FaName")),
               ("محل فاتحه", val("FaFatehaPlace")),
               ("تعداد ایتام", val("OrphanCount"))])
    kv_row(t, [("نام پدر", val("FaFather")),
               ("علت وفات", val("FaDeathCause")),
               ("محل وفات", val("FaDeathPlace"))])
    kv_row(t, [("مشهور به", val("FaKnownAs")),
               ("تاریخ وفات", val("FaDeathDate")),
               ("سیادت", chks([("FaSD_Aam", "عام"), ("FaSD_Sadat", "سادات")]))])
    kv_row(t, [("شماره تذکره", val("FaTazkira")),
               ("سکونت اصلی", val("FaOriginRes")),
               ("مذهب", chks([("FaRG_Shia", "اهل تشیع"), ("FaRG_Sunni", "اهل تسنن")]))])
    kv_row(t, [("وضعیت مادر", chks([("MO_Alive", "در قید حیات"), ("MO_Dead", "متوفی"),
                                    ("MO_Remarried", "ازدواج مجدد")], size=8)),
               ("سرپرست فعلی اطفال", val("GuardianName")),
               ("نسبت سرپرست", val("GuardianRelation"))])
    spacer(doc, 5)

    # ── مشخصات عائله ────────────────────────────────────────────────────
    section_bar(doc, "مشخصات عائله", note="(حداکثر ۸ عضو — ادامه در برگهٔ پیوست)")
    cols = [400, 1620, 1400, 1400, 1080, 840, 840, 1590, 1590]
    cols[-1] = CONTENT_W - sum(cols[:-1])
    t = table(doc, cols, cell_margin=30)
    r = row(t, height=225)
    heads = ["ردیف", "نام و تخلص", "نام پدر", "شماره تذکره", "تاریخ تولد",
             "جنسیت", "سیادت", "تحصیلات", "وضعیت جسمی"]
    for i, h in enumerate(heads):
        shade(r.cells[i], NAVY)
        vcenter(r.cells[i])
        text_para(r.cells[i], h, bold=True, size=8.5, color="FFFFFF", align="center")
    for n in range(1, 9):
        r = row(t, height=1)
        vcenter(r.cells[0])
        text_para(r.cells[0], str(n), bold=True, size=8.5, align="center", color=MUTED)
        for i, suffix in enumerate(["Name", "Fath", "Tazk", "Bir", "Gen",
                                    "Sad", "Edu", "Phy"], start=1):
            vcenter(r.cells[i])
            p = para(r.cells[i], "center", line_exact=CELL_LINE)
            run(p, tok("F%d%s" % (n, suffix)), size=8)
    spacer(doc, 5)

    # ── بلوکِ پایانی: عکس، امضاء، مهر ───────────────────────────────────
    closing_block(doc, "محل الصاق عکس جمعی خانواده", [
        ("محل امضاء و شصت درخواست کننده",
         "اینجانب صحت معلومات فوق را تایید می‌نمایم."),
        ("امضاء و مهر مسئول پذیرش", "تکمیل کننده فورم: " + tok("FilledBy")),
    ], height=1600)
    spacer(doc, 4)

    docs_checklist(doc, "مدارک مورد نیاز:", [
        ("DK_Identity",    "کپی تذکره سرپرست و افراد تحت تکفل"),
        ("DK_FamilyPhoto", "عکس پرسنلی خانواده"),
        ("DK_DeathDoc",    "کپی مدارک فوت پدر ایتام"),
        ("DK_SurveyForm",  "فورم تحقیق و بررسی"),
    ])
    footer(doc, "فورم ۱ / درخواست ایتام")
    return save(doc, OUT, "فورم ۱ - درخواست ایتام.docx", MAN)


if __name__ == "__main__":
    path, names = build()
    print(len(names))
