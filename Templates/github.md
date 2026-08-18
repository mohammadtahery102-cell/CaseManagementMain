repo: mohammadtahery102-cell/CaseManagementMain
branch: master

## Last sync
date: 2026-08-15T11:03:21Z
commit: 05fcdd5db8de

### Updated in this project
- Designed "برگه دریافتی مساعدت" (aid receipt) — two-part ticket (recipient copy + office stub) with anti-forgery layers (barcode, watermark, serial, microprint, hologram strip).
- Built a static, data-driven print bundle (`assistance-receipt/`) mirroring the repo's existing `GuardianCardIntegration` architecture (frozen HTML/CSS/JS package + JSON contract), ready for a C# `AssistanceReceiptIntegration` module to consume.
- Read `GuardianCardIntegration/*`, `Models/AssistanceModel.cs`, `Models/CaseModel.cs`, `Helpers/AfghanGeoData.cs`, `Helpers/PrintHelper.cs` to ground the design and the field-mapping doc in the real schema/patterns.

## Screen map
| Project artifact | Repo files it's grounded in |
| --- | --- |
| برگه دریافتی مساعدت.dc.html (design) | GuardianCardIntegration/* (pattern reference), Models/CaseModel.cs, Models/AssistanceModel.cs, Helpers/AfghanGeoData.cs |
| assistance-receipt/ (production bundle + docs/FIELD_MAPPING.md) | GuardianCardIntegration/CardService.cs, CaseCardRepository.cs, GuardianCardRenderer.cs, Code128Barcode.cs (reuse target), Helpers/PrintHelper.cs |
