# CV Management System

A web application for recruitment: Recruiters build reusable attribute-driven Position
templates, Candidates maintain a single profile, and CVs are generated automatically from
that profile for each position they apply to. Built for the course spec in
[`pr_cv_management.md`](pr_cv_management.md).

## Architecture

A classic layered ASP.NET Core MVC application. Each layer only talks to the one below it:

| Layer | Folder | Responsibility |
|---|---|---|
| **Views (Razor)** | `Views/`, `ViewComponents/`, `wwwroot/` | Razor views, Bootstrap 5, role-aware navigation, Chart.js statistics, the shared checkbox + toolbar table component |
| **Controllers** | `Controllers/`, `Hubs/` | HTTP endpoints, `[Authorize(Roles = ...)]`, ownership checks, orchestration; SignalR hub as thin transport |
| **Service layer** | `Services/Abstractions` + `Services/Implementations` | One interface per concern: access-rule evaluation, CV rendering, throttled auto-save, search index, PDF/CSV export, badges, email, upload validation, background reminders |
| **Models & ViewModels** | `Models/`, `ViewModels/` | EF Core entities (see `UserAttributeValue.cs` for the EAV design) and view-facing data shapes |
| **Data access** | `Data/` | `ApplicationDbContext`, per-entity configurations, EF Core code-first migrations (SQL Server), idempotent seed |

**Cross-cutting:** cookie authentication (ASP.NET Core Identity + Google/GitHub OAuth, email
confirmation) · role- and ownership-based authorization · centralized image-upload validation ·
background draft-CV reminders · optimistic locking via `rowversion` · EN/RU localization ·
light/dark theme · PDF (with QR code) and CSV export.

## Tech stack

- **Backend:** .NET 10, ASP.NET Core MVC, C#, Entity Framework Core, ASP.NET Core Identity, SignalR
- **Data & integrations:** SQL Server Express, Google OAuth 2.0, GitHub OAuth, Cloudinary (direct
  browser upload), Lucene.NET full-text search (SQL Server Full-Text Search isn't installed on the
  dev machine this was built on; the spec explicitly allows either)
- **Frontend:** Razor views, Bootstrap 5, Chart.js, EasyMDE (Markdown editor), Tagify (tags)
- **Services:** QuestPDF + QRCoder (PDF export), MailKit SMTP, Markdig + HtmlSanitizer

## Running it locally

1. **Database.** Point `ConnectionStrings:DefaultConnection` in `appsettings.json` (or an
   override in `appsettings.Development.json` / user-secrets) at a SQL Server instance you
   can create a database on. Default assumes a local `SQLEXPRESS` instance with Windows auth.
2. Restore the local `dotnet-ef` tool (only needed to create new migrations): `dotnet tool restore`.
3. From `src/CvManagement.Web`, just run:
   ```
   dotnet run
   ```
   Migrations and seed data apply automatically on startup (roles, attribute categories, the
   four built-in Me-tab attributes, a handful of library attributes drawn from the spec's own
   worked example; demo accounts only if enabled -- see below). The Lucene search index also rebuilds from
   the database on every startup, so it's always consistent even after direct DB edits.
4. Open `http://localhost:5150` (the default `http` launch profile; `https://localhost:7170` with
   `dotnet run --launch-profile https`).

### Accounts

No accounts are seeded by default. Register through the app (email confirmation is required),
then have an existing Administrator grant roles on the **Users** page.

**Demo accounts (local testing only).** Setting `Seed:DemoAccounts` to `true` makes startup
create one account per role, all with the public password `Demo@12345`:
`candidate@demo.local`, `recruiter@demo.local`, `admin@demo.local`. Never enable this on a real
installation. With the setting off, deleted demo accounts stay deleted.

## Optional configuration

The app runs fully without any of this -- these just light up specific features.

### Google / GitHub sign-in

Social login is a core requirement, but I can't create OAuth apps on your behalf. To enable:

1. Register an OAuth app with each provider, with a redirect (callback) URI of
   `http://localhost:5150/signin-google` (Google Cloud → Google Auth Platform → Clients, type
   "Web application"; add your account under Audience → Test users while in Testing mode) and
   `http://localhost:5150/signin-github` (GitHub → Settings → Developer settings → OAuth Apps).
   Use your deployed address instead of `http://localhost:5150` in production. The GitHub app
   requests the `user:email` scope, so accounts with a private email still work.
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

### Email (confirmation + reminders)

Set `Email:SmtpHost`, `Email:SmtpPort`, `Email:SmtpUsername`, `Email:SmtpPassword`, `Email:FromAddress`
via user-secrets. Without an SMTP host, emails are written to the log instead (the registration page
also shows the confirmation link), so every flow still works locally.

### Draft CV reminders

`DraftCvReminderService` (a hosted `BackgroundService`) wakes up every `Reminders:DraftCv:Interval` and
emails candidates whose CV has been a Draft longer than `DraftAge`, at most once per `RepeatAfter`.
Set `Reminders:DraftCv:BaseUrl` to include direct links, or `Enabled: false` to switch it off.

### Upload rules

`Uploads:Images` (`AllowedFormats`, `MaxFileSizeBytes`) configures both the Cloudinary widget and the
server-side check (`IUploadValidator`) that every stored image URL comes from this app's own
Cloudinary account in an allowed format.

## Project structure

```
src/
  CvManagement.Web/
    Models/                  EF Core entities + enums (see UserAttributeValue.cs for the EAV design)
    ViewModels/              View-facing data shapes, one folder per feature
    Data/                    DbContext, entity configurations, migrations, seed data
    Services/
      Abstractions/           Interfaces -- one per concern (IPositionAccessEvaluator,
                               ICvRenderService, IProfileAutoSaveService, IUploadValidator, ...)
      Implementations/        incl. DraftCvReminderService (hosted background job)
    Controllers/, Views/, ViewComponents/
    Hubs/DiscussionHub.cs      SignalR, thin transport only
    wwwroot/js/                autosave.js, table-toolbar.js, attribute-picker.js, discussion.js, home-charts.js
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

- **Deletes are set-based and cascade in the database.** Deleting a position removes its CVs, likes,
  template and discussion via `ON DELETE CASCADE` in one statement -- no "delete children in a loop".
- **No queries inside loops.** Eligibility for any number of (candidate, position) pairs is evaluated
  with two queries (`IPositionAccessEvaluator.FilterEligibleAsync`); auto-save loads a whole batch
  with two queries and commits it in one `SaveChanges`, dropping only the fields whose version is stale.
- **Built-in attributes are identified by `SystemKey`, not by name**, so Recruiters can rename
  "First Name" without breaking display-name sync, OAuth sign-up or seeding.
- **Blocking a user rotates their security stamp**, and stamps are re-validated every minute, so a
  block or role change also ends sessions that are already open.

## Optional requirements

All five are implemented: PDF export with a QR code back to the CV, email-confirmed password
registration, SVG badge panel (downloadable), per-attribute validation tuning (length, regex,
numeric range), and CSV export of a position's CVs (formula-injection safe).
