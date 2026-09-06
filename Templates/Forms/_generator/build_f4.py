# -*- coding: utf-8 -*-
"""
فورم شماره ۴ — تحقیق و بررسی (بازدید میدانی)

محورِ اصلی طبقِ خواستِ کارفرما: «اقتصادِ خانواده». بخش‌های نیمه‌کارهٔ فورمِ
اصلی (سطح درآمد خانواده و وضعیت جسمی که هرکدام فقط یک گزینه داشتند) اینجا
به سیاههٔ کاملِ گزینه‌ها باز شده‌اند.
"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from formkit import *   # noqa

OUT = sys.argv[1] if len(sys.argv) > 1 else "."
MAN = sys.argv[2] if len(sys.argv) > 2 else None

SIX = six_widths()
LBL = 1560
TWO = [LBL, CONTENT_W - LBL]


def crow(t, cap, items, size=8):
    r = row(t)
    label_cell(r.cells[0], cap)
    choice_cell(r.cells[1], items, size=size)
    return r


def build():
    reset_tokens()
    doc = new_document()

    header_block(
        doc, "فورم شماره ۴ — تحقیق و بررسی",
        right_rows=[("ولایت", "Province"), ("ولسوالی", "District")],
        left_rows=[("تاریخ بازدید", "SurveyDate"), ("کد اختصاصی", "Code"),
                   ("نوبت بازدید", "VisitNo")],
        subtitle="بازدید میدانی و ارزیابی وضعیت اقتصادی خانواده")

    # ── شناسهٔ پرونده ────────────────────────────────────────────────────
    t = table(doc, SIX)
    kv_row(t, [("اسم متقاضی", val("HeadName")),
               ("اسم پدر", val("HeadFather")),
               ("شماره پرونده", val("CaseNo"))])
    r = row(t)
    label_cell(r.cells[0], "نوع پرونده")
    c = merge(r, 1, 3)
    choice_cell(c, [("C_Orphan", "ایتام"), ("C_Disabled", "معلولین"),
                    ("C_Migrant", "مهاجرین"), ("C_Widow", "ارامل"),
                    ("C_BadGuardian", "بدسرپرست"), ("C_Vulnerable", "آسیب‌پذیر"),
                    ("C_Other", "متفرقه")], size=8)
    label_cell(r.cells[4], "افراد تحت تکفل")
    value_cell(r.cells[5], "Dependents")
    r = row(t)
    label_cell(r.cells[0], "آدرس محل بازدید")
    value_cell(merge(r, 1, 5), "CurrentRes")
    spacer(doc, 3)

    # ── وضعیت اقتصادی خانواده ───────────────────────────────────────────
    section_bar(doc, "وضعیت اقتصادی خانواده", note="(محور اصلی بررسی)")
    t = table(doc, TWO)
    crow(t, "منابع درآمد", [("IS_Daily", "کارگری روزمزد"), ("IS_Vendor", "دستفروشی"),
                            ("IS_Farm", "دهقانی"), ("IS_Livestock", "مالداری"),
                            ("IS_Craft", "حرفه و صنعت"), ("IS_Salary", "معاش دولتی"),
                            ("IS_Relatives", "کمک اقارب"),
                            ("IS_Charity", "کمک مؤسسات"), ("IS_None", "بدون درآمد")])
    crow(t, "سطح درآمد ماهانه", [("INC_VeryLow", "زیر ۲۰۰۰ افغانی"),
                                 ("INC_Low", "۲۰۰۰ تا ۵۰۰۰"),
                                 ("INC_Mid", "۵۰۰۱ تا ۱۰۰۰۰"),
                                 ("INC_High", "بالاتر از ۱۰۰۰۰ افغانی")])
    crow(t, "کفایت درآمد", [("SUF_No", "کمتر از نیازهای اساسی"),
                            ("SUF_Barely", "به‌سختی کفایت می‌کند"),
                            ("SUF_Yes", "کفایت می‌کند")])
    t = table(doc, SIX)
    kv_row(t, [("افراد نان‌آور", val("EarnersCount")),
               ("درآمد ماهانه", val("MonthlyIncome")),
               ("سرانه ماهانه", val("IncomePerCapita"))])
    kv_row(t, [("مصرف خوراک", val("ExpFood")),
               ("مصرف کرایه", val("ExpRent")),
               ("مصرف درمان", val("ExpHealth"))])
    kv_row(t, [("مصرف تعلیم", val("ExpEducation")),
               ("سایر مصارف", val("ExpOther")),
               ("مجموع مصارف", val("ExpTotal"))])
    r = row(t)
    label_cell(r.cells[0], "قرضداری")
    choice_cell(r.cells[1], [("DBT_Yes", "دارد"), ("DBT_No", "ندارد")])
    label_cell(r.cells[2], "مبلغ قرض")
    value_cell(r.cells[3], "DebtAmount")
    label_cell(r.cells[4], "دلیل قرض")
    value_cell(r.cells[5], "DebtReason")
    spacer(doc, 3)

    # ── وضعیت مسکن و دارایی ─────────────────────────────────────────────
    section_bar(doc, "وضعیت مسکن، دارایی، صحت و تعلیم")
    t = table(doc, TWO)
    crow(t, "نوع مسکن", [("HS_Own", "شخصی"), ("HS_Rent", "کرایی"),
                         ("HS_Gerawi", "گروی"), ("HS_Sawabi", "ثوابی"),
                         ("HS_Tent", "خیمه / سرپناه موقت")])
    crow(t, "امکانات مسکن", [("FC_Water", "آب آشامیدنی"), ("FC_Power", "برق"),
                             ("FC_Toilet", "تشناب"), ("FC_Kitchen", "آشپزخانه"),
                             ("FC_Heating", "وسیله گرمایشی")])
    crow(t, "دارایی‌ها", [("AS_Land", "زمین زراعتی"), ("AS_House", "خانه ملکیت"),
                          ("AS_Vehicle", "وسیله نقلیه"), ("AS_Livestock", "مواشی"),
                          ("AS_None", "هیچکدام")])
    t = table(doc, SIX)
    kv_row(t, [("مبلغ کرایه ماهانه", val("RentAmount")),
               ("تعداد اتاق", val("RoomCount")),
               ("تعداد ساکنین", val("ResidentCount"))])
    spacer(doc, 2)
    t = table(doc, TWO)
    crow(t, "وضعیت صحت", [("HL_Healthy", "همه سالم"),
                                ("HL_Chronic", "بیمار مزمن در خانواده"),
                                ("HL_Disabled", "معلول در خانواده"),
                                ("HL_Critical", "بیمار نیازمند درمان فوری")])
    crow(t, "دسترسی صحی", [("MA_Easy", "آسان"), ("MA_Hard", "دشوار"),
                                    ("MA_None", "ندارد")])
    t = table(doc, SIX)
    kv_row(t, [("نام بیمار / بیماری", val("PatientName")),
               ("مصرف درمان", val("MedicalCost")),
               ("تعداد معلولین", val("DisabledCount"))])
    kv_row(t, [("اطفال سن مکتب", val("SchoolAgeCount")),
               ("شامل مکتب", val("InSchoolCount")),
               ("بازمانده از تحصیل", val("DropoutCount"))])
    r = row(t)
    label_cell(r.cells[0], "دلیل بازماندگی")
    value_cell(merge(r, 1, 5), "DropoutReason")
    spacer(doc, 3)

    # ── ارزیابی بررسی کننده ─────────────────────────────────────────────
    section_bar(doc, "ارزیابی، نتیجه بررسی و استعلام")
    t = table(doc, TWO)
    crow(t, "سطح آسیب‌پذیری", [("VB_High", "پرخطر"), ("VB_Mid", "متوسط"),
                               ("VB_Low", "کم‌خطر")])
    crow(t, "اولویت پیشنهادی", [("PR1", "اولویت اول"), ("PR2", "اولویت دوم"),
                                ("PR3", "اولویت سوم")])
    crow(t, "نتیجه بررسی", [("V_Approve", "تأیید پرونده"), ("V_Reject", "رد پرونده"),
                            ("V_Refer", "ارجاع به استعلام")])
    t = table(doc, [CONTENT_W], border_color=GRID_SOFT, cell_margin=80)
    r = row(t, height=900)
    p = para(r.cells[0], "right", space_before=2, line_exact=CELL_LINE)
    run(p, "شرح نتیجه تحقیق و بررسی: ", bold=True, size=9, color=NAVY)
    p = para(r.cells[0], "both", line_exact=230)
    run(p, tok("SurveyResult"), size=9)
    spacer(doc, 3)
    t = table(doc, SIX)
    kv_row(t, [("اسامی بررسی کننده‌ها", val("Surveyors")),
               ("مسئول بررسی", val("SurveyManager")),
               ("تاریخ بررسی", val("SurveyDate"))])
    spacer(doc, 2)
    t = table(doc, SIX)
    kv_row(t, [("محل استعلام", val("InquiryPlace")),
               ("شماره استعلام", val("InquiryNo")),
               ("تاریخ دریافت", val("InquiryDate"))])
    r = row(t)
    label_cell(r.cells[0], "مسئول پاسخگو")
    value_cell(merge(r, 1, 3), "InquiryResponder")
    label_cell(r.cells[4], "نتیجه")
    choice_cell(r.cells[5], [("IQ_Approve", "تأیید"), ("IQ_Reject", "رد")])
    spacer(doc, 3)

    # ── تصدیق فرد معتمد ─────────────────────────────────────────────────
    section_bar(doc, "محل تصدیق فرد معتمد", note="(اختیاری)")
    t = table(doc, [CONTENT_W], border_color=GRID_SOFT, cell_margin=90)
    r = row(t, height=500)
    p = para(r.cells[0], "both", space_before=2, line_exact=250)
    run(p, "اینجانب ", size=9)
    dotted(p, 26)
    run(p, " فرزند ", size=9)
    dotted(p, 22)
    run(p, " با وظیفه ", size=9)
    dotted(p, 22)
    run(p, " و شماره تماس ", size=9)
    dotted(p, 20)
    run(p, " بدینوسیله گواهی و تأیید می‌نمایم که:", size=9)
    for _ in range(2):
        p = para(r.cells[0], "both", line_exact=250)
        dotted(p, 118)
    p = para(r.cells[0], "left", space_before=3, line_exact=250)
    run(p, "امضاء یا شصت فرد معتمد: ", bold=True, size=9, color=NAVY)
    run(p, "_" * 28, size=9, color=MUTED)
    spacer(doc, 3)

    closing_block(doc, None, [
        ("امضاء مسئول بررسی و سروی", None),
        ("امضاء و مهر مدیر پروژه", None),
        ("تاریخ ثبت در سیستم", tok("AcceptDate")),
    ], height=640)

    footer(doc, "فورم ۴ / تحقیق و بررسی")
    return save(doc, OUT, "فورم ۴ - تحقیق و بررسی.docx", MAN)


if __name__ == "__main__":
    print(len(build()[1]))
