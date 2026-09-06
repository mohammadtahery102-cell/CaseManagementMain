# Disability Module — Handover Summary

**For management and end users** · 4 September 2026

---

## What was done

The disability section of the case management system was reviewed from end to
end and prepared for daily use. Fifteen problems were identified. Eight were
fixed, including the two most serious. The rest are documented with a
recommended fix and none of them block use of the system.

---

## The two serious problems — and what they meant in practice

### 1. Every disability record showed a false card date

Whenever a disability case was saved, the system quietly recorded that the
person's disability card had been **issued that day and expired that same day** —
even when no card date had been entered. Nobody typed these dates; the system
invented them.

The same fault recorded a false date of death for the father on every orphan
case.

**Now fixed.** These dates can be left empty, and empty means empty. For a
beneficiary who has no disability card, the fields *should* be left blank.

> **Please note:** records created *before* this update still contain the false
> dates. They were not changed, because deleting data requires your explicit
> approval. We have provided a query that counts exactly how many records are
> affected. **Please decide whether to clear them.**

### 2. Disabled people without a government card could not receive service

The system required a **photo of the disability card** before a case could be
activated — even for beneficiaries who do not have a card. Since the case could
not be activated, the person could not receive assistance at all.

This affected the most vulnerable applicants: those who had not yet obtained
official documentation.

**Now fixed.** The card photo is required only when the case states that the
person *has* a card. If the case says "ندارد" (does not have) or "در حال اقدام"
(in progress), the photo is optional and the case can be activated normally.

---

## Other improvements

| Improvement | What it means for you |
|---|---|
| **New "تاریخچه" (History) tab** | Every case now has a history tab showing what changed, from what value to what value, by whom, and when. This information was being recorded all along but was never visible |
| **Detailed change tracking** | Editing a disability degree now records "changed from اول to سوم", not just "something was edited" |
| **Reports and exports completed** | Excel and Word exports now include all 11 disability fields. Previously only 2 appeared |
| **Search improved** | If an administrator adds a new disability type, it is now searchable. Previously it could be entered but never found |
| **Multi-branch data fixed** | Clearing a disability record at a branch office now correctly reaches the head office. Previously the old record stayed behind and could reappear |
| **Legal representative (وکیل)** | Disability cases can now record two legal representatives with name, relationship, ID, phone, address, photo and notes — searchable and printable |

---

## What the system does *not* do

Please make sure staff understand these limits:

1. **The system records a government disability card. It does not issue one.**
   There is no card printing, numbering, renewal or replacement for disability
   cards.
2. **The printed beneficiary card's issue and expiry dates are generated at the
   moment of printing.** They are not a record of when a card was issued, and
   they will differ each time the card is reprinted.
3. **The printed case report shows only disability type and degree.** For the
   complete disability record, use the **Excel or Word export**.
4. **There is no dedicated disability report.** Use the report builder or the
   Excel export.
5. **Legal representatives appear on disability cases only** — not on orphan,
   migrant or elderly cases.

---

## Before you start using it

1. **Decide about the false historical dates** (see above) — this is the one
   decision that needs you.
2. **Test on one real laptop first:** create a disability case with card status
   "ندارد", confirm it can be activated; change a disability degree and confirm
   the History tab shows the old and new values; add a legal representative and
   confirm it saves.
3. **Tell anyone who uses the Excel export** that 9 new columns were added. If
   they have a spreadsheet or macro that reads columns by position, it needs
   updating.

---

## Overall assessment

**Ready for use, with minor known risks.**

The disability section is materially safer than before this review. The two
faults that were actively harming data and blocking beneficiaries are fixed and
tested. The remaining items are documented, none prevent daily use, and each has
a recommended fix for a future update.

**One technical condition applies before release:** the development team must
stop making changes, rebuild the software once, and run the automated test suite
once against that final build. Testing was repeatedly interrupted by ongoing
development, so a final confirmation run on a frozen version is needed. This is a
scheduling matter, not a fault in the software.

---

### Reference documents

| Document | Audience |
|---|---|
| `RELEASE_PACKAGE.md` | Full release package and verdict |
| `DISABILITY_RELEASE_REPORT.md` | Complete technical findings |
| `H4_IMPACT_ASSESSMENT.md` | One open issue, analysed, awaiting a decision |
| `LAWYER_FEATURE_AUDIT.md` | Legal representative feature review |
| `DISABILITY_MODIFIED_FILES.md` | Every file changed, with risk rating |
