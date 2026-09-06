# -*- coding: utf-8 -*-
"""فورم شماره ۲ — درخواست نیازمندان (معلول / مهاجر / ارامل / متفرقه)"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from formkit import *   # noqa

OUT = sys.argv[1] if len(sys.argv) > 1 else "."
MAN = sys.argv[2] if len(sys.argv) > 2 else None

SIX = six_widths()


def build():
    reset_tokens()
    doc = new_document(margins=(0.6, 0.5, 0.85, 0.85))

    header_block(
        doc, "فورم شماره ۲ — درخواست نیازمندان",
        right_rows=[("ولایت", "Province"), ("ولسوالی", "District")],
        left_rows=[("تاریخ ثبت", "AcceptDate"), ("کد اختصاصی", "Code"),
                   ("شماره پرونده", "CaseNo")],
        subtitle="بخش رسیدگی به معلولین، مهاجرین، ارامل و آسیب‌پذیران", height=400)

    # ── نوارِ اولویت و نوعِ پرونده ────────────────────────────────────────
    t = table(doc, [CONTENT_W], border_color=GRID_SOFT, cell_margin=80)
    r = row(t, height=220)
    shade(r.cells[0], BAND_BG)
    vcenter(r.cells[0])
    p = para(r.cells[0], "right", space_before=2, space_after=2)
    run(p, "اولویت‌بندی: ", bold=True, size=9, color=NAVY)
    for tk, cap in (("PR1", "اول"), ("PR2", "دوم"), ("PR3", "سوم")):
        checkbox(p, tk, cap, size=8.5, gap="    ")
    run(p, "      نوع پرونده: ", bold=True, size=9, color=NAVY)
    for tk, cap in (("C_Disabled", "معلول"), ("C_Migrant", "مهاجر"),
                    ("C_Widow", "ارامل"), ("C_Elderly", "کهنسال"),
                    ("C_Other", "متفرقه")):
        checkbox(p, tk, cap, size=8.5, gap="    ")
    run(p, ": ", size=8.5)
    run(p, tok("RequestTypeName"), size=8.5)
    spacer(doc, 2)

    # ── اطلاعات سرپرست ──────────────────────────────────────────────────
    section_bar(doc, "اطلاعات سرپرست", height=160)
    t = table(doc, SIX)
    kv_row(t, [("نام و تخلص", val("HeadName")),
               ("افراد تحت تکفل", val("Dependents")),
               ("سیادت", chks([("SD_Aam", "عام"), ("SD_Sadat", "سادات")]))])
    kv_row(t, [("نام پدر", val("HeadFather")),
               ("تاریخ تولد", val("HeadBirthDate")),
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
    kv_row(t, [("نوع سند هویتی", val("HeadIdCardType")),
               ("مهارت خاص", val("Skill")),
               ("شرح وضعیت جسمی", val("PhysicalNotes"))])
    r = row(t)
    label_cell(r.cells[0], "سکونت اصلی")
    value_cell(merge(r, 1, 5), "OriginRes")
    r = row(t)
    label_cell(r.cells[0], "سکونت فعلی")
    value_cell(merge(r, 1, 3), "CurrentRes")
    label_cell(r.cells[4], "لینک موقعیت")
    value_cell(r.cells[5], "LocationLink", size=8)
    spacer(doc, 2)

    # ── اطلاعات معلول ───────────────────────────────────────────────────
    section_bar(doc, "اطلاعات معلول", note="(در صورت معلولیت تکمیل گردد)", height=160)
    LBL = 1420
    t = table(doc, [LBL, CONTENT_W - LBL])
    def crow(cap, items, size=8):
        r = row(t)
        label_cell(r.cells[0], cap)
        choice_cell(r.cells[1], items, size=size)
    crow("نوع معلولیت", [("D_Phys", "جسمی (حرکتی)"), ("D_Hear", "شنوایی"),
                         ("D_Vis", "بینایی"), ("D_Ment", "ذهنی / روانی"),
                         ("D_Multi", "چندگانه")])
    crow("شدت معلولیت", [("DS_Mild", "خفیف"), ("DS_Mod", "متوسط"),
                         ("DS_Severe", "شدید"),
                         ("DS_VSevere", "خیلی شدید (نیازمند مراقبت کامل)")])
    crow("نیازهای اصلی", [("DN_Rehab", "درمانی و توان‌بخشی"),
                          ("DN_Aids", "وسایل کمکی (عصا، ویلچر، سمعک، عینک)"),
                          ("DN_Fin", "حمایت مالی"), ("DN_House", "مسکن مناسب")])
    t2 = table(doc, SIX)
    kv_row(t2, [("دلیل معلولیت", val("DisabilityCause")),
                ("کارت معلولیت", chks([("DC_Yes", "دارد"), ("DC_No", "ندارد"),
                                       ("DC_Pending", "در حال اقدام")])),
                ("شماره کارت", val("DisabilityCardNo"))])
    r = row(t2)
    label_cell(r.cells[0], "شرح معلولیت")
    value_cell(merge(r, 1, 3), "DisabilityDescription")
    label_cell(r.cells[4], "نیازهای خاص")
    value_cell(r.cells[5], "SpecialNeeds")
    spacer(doc, 2)

    # ── اطلاعات مهاجر ───────────────────────────────────────────────────
    section_bar(doc, "اطلاعات مهاجر", note="(در صورت مهاجرت تکمیل گردد)", height=160)
    t = table(doc, SIX)
    kv_row(t, [("نوع سند", chks([("MD_Gov", "دولتی"), ("MD_Intl", "بین‌المللی"),
                                 ("MD_Other", "سایر")])),
               ("سند شناسایی", chks([("MID_Tazkira", "تذکره"), ("MID_Passport", "پاسپورت"),
                                     ("MID_None", "هیچکدام")])),
               ("مسکن", chks([("MH_Own", "شخصی"), ("MH_Rent", "کرایه"),
                              ("MH_Sawabi", "ثوابی"), ("MH_Gerawi", "گروی")]))])
    kv_row(t, [("اسم سند", val("MDocName")),
               ("کشور مبدأ", val("OriginCountry")),
               ("مدت اقامت خارج", val("OutsideDuration"))])
    kv_row(t, [("شماره سند", val("MDocNo")),
               ("کشور مقصد", val("DestinationCountry")),
               ("مدت اقامت داخل", val("InsideDuration"))])
    kv_row(t, [("تاریخ خروج", val("DepartureDate")),
               ("تاریخ برگشت", val("ArrivalDate")),
               ("مدت مساعدت (ماه)", val("AssistanceMonths"))])
    kv_row(t, [("حالت خروج", chks([("ME_Forced", "اجباری"), ("ME_Voluntary", "اختیاری")])),
               ("وضعیت اقامت", chks([("MS_Temp", "موقت"), ("MS_Perm", "دائم"),
                                     ("MS_Unknown", "نامعلوم")])),
               ("نیاز اولیه", val("PrimaryNeed"))])
    spacer(doc, 2)

    # ── مشخصات عائله ────────────────────────────────────────────────────
    section_bar(doc, "مشخصات عائله", note="(در صورت نیاز)", height=160)
    cols = [400, 1750, 1500, 1560, 1180, 850, 1760, 1760]
    cols[-1] = CONTENT_W - sum(cols[:-1])
    t = table(doc, cols, cell_margin=30)
    r = row(t, height=225)
    for i, h in enumerate(["ردیف", "نام و تخلص", "نام پدر", "شماره تذکره",
                           "تاریخ تولد", "جنسیت", "تحصیلات", "وضعیت جسمی"]):
        shade(r.cells[i], NAVY)
        vcenter(r.cells[i])
        text_para(r.cells[i], h, bold=True, size=8.5, color="FFFFFF", align="center")
    for n in range(1, 9):
        r = row(t)
        vcenter(r.cells[0])
        text_para(r.cells[0], str(n), bold=True, size=8.5, align="center", color=MUTED)
        for i, suffix in enumerate(["Name", "Fath", "Tazk", "Bir", "Gen", "Edu", "Phy"],
                                   start=1):
            vcenter(r.cells[i])
            p = para(r.cells[i], "center", line_exact=CELL_LINE)
            run(p, tok("F%d%s" % (n, suffix)), size=8)
    spacer(doc, 2)

    # ── شرح درخواست ─────────────────────────────────────────────────────
    # آموزش — چرا اینجا شکستِ صفحه: با جدولِ عائلهٔ ۸ ردیفی این فورم در یک
    # صفحه جا نمی‌شود. بدونِ شکستِ دستی، مرزِ صفحه وسطِ همین بخش می‌افتاد و
    # صفحهٔ دوم فقط بلوکِ امضاء را نشان می‌داد. با شکست، صفحهٔ دوم یک برگهٔ
    # کاملِ «شرح درخواست + امضاء + مدارک» است و کادرِ شرح هم بزرگ می‌شود.
    page_break(doc)
    section_bar(doc, "شرح درخواست", height=160)
    t = table(doc, [CONTENT_W], border_color=GRID_SOFT, cell_margin=80)
    r = row(t, height=6200)
    value_cell(r.cells[0], "RequestDesc", size=9)
    spacer(doc, 2)

    closing_block(doc, None, [
        ("محل امضاء و شصت درخواست کننده",
         "اینجانب صحت معلومات فوق را تایید می‌نمایم."),
        ("امضاء و مهر مسئول پذیرش", "تکمیل کننده فورم: " + tok("FilledBy")),
    ], height=700)
    spacer(doc, 2)

    docs_checklist(doc, "مدارک مورد نیاز:", [
        ("DK_Identity",   "کپی تذکره سرپرست و افراد تحت تکفل"),
        ("DK_Migration",  "کپی مدارک مهاجرت"),
        ("DK_Photo",      "عکس پرسنلی"),
        ("DK_Disability", "کپی مدارک معلولیت"),
        ("DK_SurveyForm", "فورم تحقیق و بررسی"),
    ])
    footer(doc, "فورم ۲ / درخواست نیازمندان")
    return save(doc, OUT, "فورم ۲ - درخواست نیازمندان.docx", MAN)


if __name__ == "__main__":
    print(len(build()[1]))
