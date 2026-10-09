# Sourcing

*Last updated: 2026-10-09*

Desk Sourcing records who supplies Ocharo's materials and what they shared. The first slice holds
suppliers, the routes that reach them, the people behind those routes, and the photos, PDFs and videos
they sent. It records outreach; it never sends a message. [Contract](../../engineering/architecture/sourcing-contract.md).

| Capability | Acceptance |
|---|---|
| Starting views | Suppliers for contact work; Materials for photos/files across suppliers, with supplier and inbox links |
| Personal view settings | Account/organization-scoped browser preferences for starting view, section visibility and contacts-first/materials-first order |
| Supplier list | Search by name, city, key, person or destination; filter by stage; cover photo and counts per supplier |
| Supplier record | Stage `lead` → `contacted` → `replied` → `quoted`; `qualified` and `dropped` are explicit operator decisions |
| Contact routes | WhatsApp, email, phone, form, website and social routes; one primary; shared destinations allowed |
| People | Named contacts per supplier; removing a person keeps the routes they answered on |
| Shared media | Photos, PDFs and MP4 videos per supplier; thumbnails, lightbox, captions and capture type |
| Inbox | Bulk upload first, sort later; move one or many items between the inbox and suppliers |
| Duplicate safety | Same bytes are stored once; visually near images are flagged, never merged |
| Ledger import | An explicit, re-runnable bundle import adds missing records and never overwrites Desk edits |

Viewers read; editors and owners change. Everything stays inside the organization: foreign
suppliers, people, routes and media return 404, and cross-organization references fail validation.
Original bytes, including any GPS metadata, are served only to members; the UI flags located photos.

Later slices: sourcing searches with A–E references, offers with original currency and FX snapshots,
reference-fit grading, and lead-to-qualified review evidence.

## Supplier presentation

Supplier identity and stage remain visible. Sourcing → View settings controls the optional Supplier details,
Contacts, People, and Shared materials and files sections. Saving changes only the current account's view
in this browser; it does not modify shared supplier records or grant editing permission. Existing unsaved
supplier forms must be saved or cancelled before applying layout changes.

[Supplier information analysis](../../engineering/research/desk/2026-10-09-supplier-information-and-personal-views.md)
separates existing contact/media facts from planned structured offers, samples and follow-up records.
