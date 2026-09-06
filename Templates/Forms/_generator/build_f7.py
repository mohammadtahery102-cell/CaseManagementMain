# -*- coding: utf-8 -*-
"""فورم شماره ۷ — پرونده بخش درمان"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from formkit import *   # noqa

OUT = sys.argv[1] if len(sys.argv) > 1 else "."
MAN = sys.argv[2] if len(sys.argv) > 2 else None

SIX = six_widths()

SERVICES = [
    ("۱. سرپایی (Outpatient)", [("SV_Visit", "ویزیت معمول"),
                                ("SV_ShortNoAdmit", "درمان کوتاه‌مدت بدون بستری"),
                                ("SV_Rx", "تجویز دارو")]),
    ("۲. بستری کوتاه‌مدت", [("SV_AdmitLt24", "بستری کمتر از ۲۴ ساعت"),
                            ("SV_Observation", "تحت نظر بودن / اقدامات اولیه")]),
    ("۳. بستری مدت‌دار (Inpatient)", [("SV_AdmitGt24", "بستری بیش از ۲۴ ساعت"),
                                      ("SV_HospitalCare", "نیاز به مراقبت‌های بیمارستانی")]),
    ("۴. جراحی (Surgical)", [("SV_MinorSurgery", "جراحی کوچک (بخیه، سرپایی)"),
                             ("SV_MajorSurgery", "جراحی بزرگ (اتاق عمل و بستری)")]),
    ("۵. درمان دارویی طولانی‌مدت", [("SV_Chronic", "درمان مزمن (دیابت، فشار خون، صرع)"),
                                    ("SV_RepeatRx", "نیاز به نسخه‌های تکراری")]),
    ("۶. توان‌بخشی / فیزیوتراپی", [("SV_Physio", "فیزیوتراپی"),
                                   ("SV_Rehab", "توان‌بخشی")]),
    ("۷. ارجاع به مرکز تخصصی", [("SV_Referral", "ارجاع به مرکز مجهزتر")]),
    ("۸. پیگیری و مراقبت دوره‌ای", [("SV_FollowUp", "مراجعات منظم برای چکاپ")]),
    ("۹. مراقبت حمایتی / روانی", [("SV_Counseling", "مشاوره روانشناسی")]),
]


def build():
    reset_tokens()
    doc = new_document()

    header_block(
        doc, "فورم شماره ۷ — پرونده بخش درمان",
        right_rows=[("ولایت", "Province"), ("ولسوالی", "District")],
        left_rows=[("تاریخ تشکیل", "AcceptDate"), ("کد اختصاصی", "Code"),
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
    run(p, "        زون: ", bold=True, size=9, color=NAVY)
    run(p, tok("Zone"), size=8.5)
    spacer(doc, 4)

    # ── مشخصات بیمار و همراه + عکس ──────────────────────────────────────
    section_bar(doc, "مشخصات بیمار، سرپرست و همراه")
    PH = 2050
    L1 = 1500
    rest = CONTENT_W - PH
    V1 = (rest - 2 * L1) // 2
    cols = [L1, V1, L1, rest - L1 - V1 - L1, PH]
    t = table(doc, cols)
    rows_spec = [(("نام بیمار و تخلص", "HeadName"), ("نام همراه بیمار", "CompanionName")),
                 (("نام پدر بیمار", "HeadFather"), ("نام پدر همراه", "CompanionFather")),
                 (("شماره تذکره بیمار", "HeadTazkira"), ("شماره تذکره همراه", "CompanionTazkira")),
                 (("شماره تماس بیمار", "HeadPhone"), ("شماره تماس همراه", "CompanionPhone")),
                 (("سکونت فعلی بیمار", "CurrentRes"), ("آدرس همراه", "CompanionAddress")),
                 (("تاریخ تولد بیمار", "HeadBirthDate"), ("نسبت همراه با بیمار", "CompanionRelation"))]
    for a, b in rows_spec:
        r = row(t)
        label_cell(r.cells[0], a[0]); value_cell(r.cells[1], a[1])
        label_cell(r.cells[2], b[0]); value_cell(r.cells[3], b[1])
    merged = vmerge_column(t, len(cols) - 1)
    shade(merged, BAND_BG)
    vcenter(merged)
    text_para(merged, "محل الصاق عکس بیمار", bold=True, size=9,
              color=NAVY, align="center")
    text_para(merged, "(۴×۳ — پس از چاپ الصاق و مهر گردد)", size=8,
              color=MUTED, align="center", space_before=2)

    t = table(doc, SIX)
    kv_row(t, [("سیادت", chks([("SD_Aam", "عام"), ("SD_Sadat", "سادات")])),
               ("مذهب", chks([("RG_Shia", "اهل تشیع"), ("RG_Sunni", "اهل تسنن")])),
               ("جنسیت", chks([("GN_Male", "مرد"), ("GN_Female", "زن")]))])
    spacer(doc, 4)

    # ── نوع خدمت درمانی ─────────────────────────────────────────────────
    section_bar(doc, "نوع خدمت درمانی مورد نیاز")
    half = CONTENT_W // 2
    t = table(doc, [half, CONTENT_W - half], border_color=GRID_SOFT)
    left_items = SERVICES[:5]
    right_items = SERVICES[5:]
    n = max(len(left_items), len(right_items))
    r = row(t)
    for idx, group in enumerate((left_items, right_items)):
        c = r.cells[idx]
        for title, items in group:
            p = para(c, "right", space_before=2, line_exact=CELL_LINE)
            run(p, title, bold=True, size=8.5, color=NAVY)
            for tk, cap in items:
                p = para(c, "right", line_exact=CELL_LINE, ind_right=200)
                checkbox(p, tk, cap, size=8)
    spacer(doc, 4)

    # ── نظر نهایی دکتر ──────────────────────────────────────────────────
    section_bar(doc, "نظر نهایی داکتر معالج")
    t = table(doc, [CONTENT_W], border_color=GRID_SOFT, cell_margin=80)
    r = row(t, height=1100)
    value_cell(r.cells[0], "DoctorOpinion", size=9)
    spacer(doc, 3)
    t = table(doc, SIX)
    kv_row(t, [("نام داکتر", val("DoctorName")),
               ("تخصص", val("DoctorSpecialty")),
               ("تاریخ", val("DoctorDate"))])
    spacer(doc, 4)

    docs_checklist(doc, "مدارک مورد نیاز:", [
        ("DK_Identity", "کپی تذکره بیمار"),
        ("DK_Medical",  "کپی مدارک درمانی"),
        ("DK_Photo",    "عکس پرسنلی"),
    ])
    spacer(doc, 3)
    docs_checklist(doc, "پرونده شامل اسکن موارد ذیل باشد:", [
        ("SC_RequestForm", "فورم درخواست"),
        ("SC_SurveyForm",  "فورم‌های بررسی"),
        ("SC_Tazkira",     "کپی تذکره بیمار"),
        ("SC_Medical",     "کپی اسناد پزشکی"),
    ])
    spacer(doc, 4)

    closing_block(doc, None, [
        ("امضاء و مهر داکتر معالج", None),
        ("امضاء و مهر مسئول بخش درمان", None),
        ("تکمیل کننده پرونده", tok("FilledBy")),
    ], height=900)

    footer(doc, "فورم ۷ / پرونده بخش درمان")
    return save(doc, OUT, "فورم ۷ - پرونده بخش درمان.docx", MAN)


if __name__ == "__main__":
    print(len(build()[1]))
