# -*- coding: utf-8 -*-
"""
پرکردنِ قالب با دادهٔ نمونه — دقیقاً همان کاری که DocxFormExport در C# می‌کند
(جایگزینیِ متنِ هر <w:t>)، تا بتوانیم *خروجیِ چاپیِ واقعی* را ببینیم نه قالبِ
پر از توکن را.
"""
import sys, os, re, shutil, random
from docx import Document

SAMPLE_TEXT = {
    "OrgName": "موسسه خیریه امداد و انکشاف",
    "Province": "کابل", "District": "پغمان",
    "AcceptDate": "1404/03/12", "PrintDate": "1404/03/20",
    "Code": "ORP-1404-0142", "CaseNo": "142", "RequestTypeName": "ایتام",
    "HeadName": "زهرا محمدی", "HeadFather": "غلام سخی", "HeadTazkira": "1401-2233-44551",
    "HeadPhone": "0700 123 456", "RelativePhone": "0788 990 112",
    "Dependents": "6", "RelationToChildren": "مادر", "Job": "خانه‌دار",
    "Education": "صنف دوازدهم", "Covered": "ندارد", "Skill": "خیاطی",
    "OriginRes": "ولایت غزنی، ولسوالی جاغوری، قریه سنگ‌ماشه",
    "CurrentRes": "کابل، ناحیه ۱۳، دشت برچی، کوچه حاجی نبی، خانه شماره ۲۴",
    "LocationLink": "34.5061, 69.1132", "PhysicalNotes": "سالم",
    "HeadIdCardType": "تذکره کاغذی", "HeadBirthDate": "1368/05/02",
    "FaName": "محمدعلی رضایی", "FaFather": "حسین‌علی", "FaKnownAs": "علی",
    "FaTazkira": "1399-1122-33445", "FaFatehaPlace": "مسجد جامع دشت برچی",
    "FaDeathCause": "حادثه ترافیکی", "FaDeathDate": "1401/08/17",
    "FaDeathPlace": "کابل", "FaOriginRes": "غزنی، جاغوری", "OrphanCount": "4",
    "GuardianName": "زهرا محمدی", "GuardianRelation": "مادر",
    "FilledBy": "احمد نظری",
    "Surveyors": "احمد نظری، فاطمه حیدری", "SurveyManager": "محمدجواد کاظمی",
    "SurveyDate": "1404/03/15", "VisitNo": "1",
}

MEMBERS = [
    ("علی رضایی", "محمدعلی", "1409-5566-77881", "1390/02/11", "پسر", "عام", "صنف ۸", "سالم"),
    ("مریم رضایی", "محمدعلی", "1409-5566-77882", "1392/06/24", "دختر", "عام", "صنف ۶", "سالم"),
    ("حسین رضایی", "محمدعلی", "1409-5566-77883", "1395/11/03", "پسر", "عام", "صنف ۳", "معلول حرکتی"),
    ("زینب رضایی", "محمدعلی", "1409-5566-77884", "1398/01/19", "دختر", "عام", "کودکستان", "سالم"),
]
SUFFIX = ["Name", "Fath", "Tazk", "Bir", "Gen", "Sad", "Edu", "Phy"]

CHECKED = "☒"
UNCHECKED = "☐"
# کدام چک‌باکس‌ها در نمونه تیک بخورند
TICKED = {"PR1", "SD_Aam", "RG_Shia", "MR_Married", "GN_Female", "PH_Healthy",
          "FaSD_Aam", "FaRG_Shia", "MO_Alive", "DK_Identity", "DK_FamilyPhoto",
          "DK_DeathDoc", "C_Orphan", "U_NonUrgent", "I_Lt2000", "INC_VeryLow",
          "HS_Rent", "HL_Healthy", "V_Approve", "MD_Gov", "MID_Tazkira",
          "MH_Rent", "MS_Perm", "ME_Forced", "D_Phys", "DS_Severe", "DN_Rehab",
          "SV_Outpatient_Visit", "SC_RequestForm", "SC_SurveyForm"}


LRM = "‎"
_ARABIC = re.compile(r"[؀-ۿ]")


def ltr(v):
    """همان قاعده‌ای که CaseFormTokens در C# اعمال می‌کند."""
    if v and any(ch.isdigit() for ch in v) and not _ARABIC.search(v):
        return LRM + v + LRM
    return v


def sample(name):
    if name in SAMPLE_TEXT:
        return ltr(SAMPLE_TEXT[name])
    m = re.match(r"^F(\d)(%s)$" % "|".join(SUFFIX), name)
    if m:
        idx, suf = int(m.group(1)) - 1, m.group(2)
        if idx < len(MEMBERS):
            return ltr(MEMBERS[idx][SUFFIX.index(suf)])
        return ""
    # چک‌باکس‌ها: نامِ کوتاه با پیشوندِ شناخته‌شده یا هر توکنِ دوحرفیِ علامت‌دار
    if re.match(r"^(PR\d|[A-Z]{1,3}_[A-Za-z0-9_]+|DK_|SC_|SV_)", name):
        return CHECKED if name in TICKED else UNCHECKED
    return ""


def fill(src, dst):
    shutil.copy(src, dst)
    doc = Document(dst)
    parts = [doc.element.body]
    for s in doc.sections:
        parts.append(s.footer._element)
        parts.append(s.header._element)
    from docx.oxml.ns import qn
    for part in parts:
        for t in part.iter(qn('w:t')):
            if t.text and "{{" in t.text:
                t.text = re.sub(r"\{\{([A-Za-z0-9_]+)\}\}",
                                lambda m: sample(m.group(1)), t.text)
    doc.save(dst)


if __name__ == "__main__":
    src_dir, dst_dir = sys.argv[1], sys.argv[2]
    os.makedirs(dst_dir, exist_ok=True)
    for f in sorted(os.listdir(src_dir)):
        if f.endswith(".docx"):
            fill(os.path.join(src_dir, f), os.path.join(dst_dir, f))
            print("filled", len(f))
