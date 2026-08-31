# CV Management System

A web application for recruitment: Recruiters build reusable attribute-driven Position
templates, Candidates maintain a single profile, and CVs are generated automatically from
that profile for each position they apply to. Built for the course spec in
[`pr_cv_management.md`](pr_cv_management.md).

## Tech stack

- **ASP.NET Core MVC (.NET 10)**, C#
- **Entity Framework Core** + **SQL Server** (optimistic concurrency via `rowversion`)
- **ASP.NET Core Identity** (roles: Candidate / Recruiter / Administrator) + Google/GitHub OAuth
- **SignalR** for live per-position discussions
- **Lucene.NET** for full-text search (SQL Server Full-Text Search isn't installed on the
  dev machine this was built on; the spec explicitly allows either)
- **Markdig** + **HtmlSanitizer** for Markdown rendering
- **Bootstrap 5**, **EasyMDE** (Markdown editor), **Tagify** (technology tags), **Cloudinary**
  upload widget (Image attribute type)

## Running it locally

1. **Database.** Point `ConnectionStrings:DefaultConnection` in `appsettings.json` (or an
   override in `appsettings.Development.json` / user-secrets) at a SQL Server instance you
   can create a database on. Default assumes a local `SQLEXPRESS` instance with Windows auth.
2. From `src/CvManagement.Web`, just run:
   ```
   dotnet run
   ```
   Migrations and seed data apply automatically on startup (roles, attribute categories, the
   four built-in Me-tab attributes, a handful of library attributes drawn from the spec's own
   worked example, and one demo account per role). The Lucene search index also rebuilds from
   the database on every startup, so it's always consistent even after direct DB edits.
3. Open `http://localhost:5199` (or whatever port `dotnet run` reports).

### Demo accounts

All seeded with password `Demo@12345`:

| Email | Role |
|---|---|
| `candidate@demo.local` | Candidate |
| `recruiter@demo.local` | Recruiter |
| `admin@demo.local` | Administrator |

## Optional configuration

The app runs fully without any of this -- these just light up specific features.

### Google / GitHub sign-in

Social login is a core requirement, but I can't create OAuth apps on your behalf. To enable:

1. Register an OAuth app with each provider, with a redirect URI of
   `https://localhost:<port>/signin-google` and `https://localhost:<port>/signin-github`
   respectively (adjust the port to match your local run).
2. Store the credentials outside source control via user-secrets (from `src/CvManagement.Web`):
   ```
   dotnet user-secrets set "Authentication:Google:ClientId" "..."
   dotnet user-secrets set "Authentication:Google:ClientSecret" "..."
   dotnet user-secrets set "Authentication:GitHub:ClientId" "..."
   dotnet user-secrets set "Authentication:GitHub:ClientSecret" "..."
   ```
   Until these are set, the sign-in page simply omits the corresponding button rather than
   erroring -- local email/password sign-in (and the seeded demo accounts) always work.

### Cloudinary (Image attribute type / profile photo)

1. Create a free Cloudinary account and an **unsigned** upload preset.
2. Set:
   ```
   dotnet user-secrets set "Cloudinary:CloudName" "..."
   dotnet user-secrets set "Cloudinary:UnsignedUploadPreset" "..."
   ```
   Until set, Image-type fields show "Image upload isn't configured yet" instead of an upload
   button -- the rest of the app is unaffected.

## Project structure

```
src/
  CvManagement.Web/
    Domain/                  EF Core entities (see UserAttributeValue.cs for the EAV design)
    Data/                    DbContext, entity configurations, migrations, seed data
    Services/
      Abstractions/           Interfaces -- one per concern (IPositionAccessEvaluator,
                               ICvRenderService, IProfileAutoSaveService, ISearchIndexService, ...)
      Implementations/
    Controllers/, Views/, ViewComponents/
    Hubs/DiscussionHub.cs      SignalR, thin transport only
    wwwroot/js/                autosave.js, table-toolbar.js, attribute-picker.js, discussion.js
  CvManagement.Tests/          Unit tests for the access-rule evaluator (the riskiest pure logic)
```

## Key design decisions (the "why" behind non-obvious choices)

- **Attributes are one wide table, not JSON or a table-per-type.** `UserAttributeValue` has one
  nullable column per data type; exactly one is populated per row, chosen by the attribute's
  `DataType`. Row *existence* is what "this attribute is on my profile" means.
- **A CV stores no content.** `Cv` is technical fields only (id/user/position/status/rowversion).
  `ICvRenderService` builds the displayed CV at read time by joining the position's template
  attributes against the candidate's `UserAttributeValue` rows. Editing a value from inside a CV
  writes to that same table, which is *why* the change is visible everywhere at once.
- **Optimistic locking is enforced everywhere that's editable**, via SQL Server `rowversion` +
  EF Core's concurrency token mechanism. The profile auto-save endpoint batches changes but
  commits each independently, so one stale field doesn't block the rest; conflicts return the
  server's current value for a "keep mine / reload" prompt rather than silently overwriting.
- **Losing position eligibility hides a CV from its own candidate, not just Recruiters** -- a
  literal reading of the spec's wording, and a genuinely easy detail to miss.
- **No per-row buttons anywhere.** Every list (Positions, Attribute Library, CVs, Users) uses the
  same checkbox-select + toolbar component (`SelectableTableToolbarViewComponent` +
  `table-toolbar.js`), satisfying the spec's explicit -20%-penalty rule in one reusable place.

## What's not implemented

The spec's **Optional Requirements** section (PDF+QR export, email-confirmation password auth,
badges/achievements, per-attribute validation tuning, CSV/Excel export) is explicitly gated on
the core being fully solid first, and hasn't been built.

Full-page i18n coverage (English/Russian) is in place for the shared layout, navigation, and
account pages; deeper coverage of every label on every page is a straightforward but large
mechanical extension of the same `IStringLocalizer<SharedResource>` pattern already used
throughout, not yet done for every view.
