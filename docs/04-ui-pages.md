# 04 - UI Pages and Component Tree

Tech stack: Blazor WebAssembly (.NET 10), Bootstrap 5 (already in project), custom CSS in `app.css`.

---

## Routing Structure

```
/                           -> Home (public event status, login CTA)
/login                      -> Login
/join                       -> Join Event / Register Profile
/event                      -> Event Home (redirect to active event)
/event/{eventId}            -> Event Home / Progress Dashboard
/event/{eventId}/dashboard  -> Personal Dashboard (Participant)
/event/{eventId}/activities -> My Activities
/event/{eventId}/leaderboard -> Leaderboards (tabs: Individual, Team)
/event/{eventId}/milestones -> Route & Milestones
/event/{eventId}/profile    -> Edit My Profile
/admin                      -> Admin Hub
/admin/event                -> Admin Event Setup
/admin/milestones           -> Admin Milestone Manager
/admin/teams                -> Admin Team Manager
/admin/participants         -> Admin Participant Manager
/admin/activities           -> Admin Activities Review (Approval Queue)
/admin/duplicates           -> Admin Duplicate Resolution
/admin/badges               -> Admin Badge Manager
/admin/reports              -> Admin Reports & Export
/admin/audit                -> Audit Log Viewer
/executive                  -> Executive Summary Dashboard
/strava/callback            -> Strava OAuth Callback Handler (no UI, auto-redirects)
/not-found                  -> 404 page
/unauthorized               -> 401/403 page
```

---

## Layout Components

### `Layout/MainLayout.razor`
- Navigation bar with role-aware links
- `EventStatusBanner` component injected below nav
- Footer
- Notification bell icon (links to notification centre)

### `Layout/NavMenu.razor`
**Participant links:** Event Home, My Dashboard, My Activities, Leaderboard, Milestones, My Profile
**TeamCaptain adds:** Team Summary
**Admin links:** Admin hub nav group (Event Setup, Activities Review, Duplicates, Reports, Audit)
**ExecutiveViewer links:** Executive Summary only
**Unauthenticated links:** Login, Home

### `Layout/EventStatusBanner.razor`
- Shows on all `/event/*` pages
- Displays event name, status chip (Draft/Active/Closed/Archived), date range
- If status = Closed: "This event has ended. Activities are read-only."
- If status = Draft: "This event is not yet active."
- Hides on admin and login pages

---

## Page: Home (`/`)

**File:** `Pages/Home.razor`
**Auth:** Anonymous
**Purpose:** Landing page for unauthenticated users or after login redirect.

**Content:**
- App hero section: event name, banner message, route teaser
- Event progress summary widget (`EventProgressWidget`)
- CTA: Login / Join Event
- If authenticated and participant: redirect to `/event/{eventId}/dashboard`
- If authenticated and admin: redirect to `/admin`

---

## Page: Login (`/login`)

**File:** `Pages/Login.razor`
**Auth:** Anonymous
**Purpose:** Email + password login form.

**Content:**
- Email / password inputs
- Submit button; shows loading spinner on submit
- Validation summary
- Link to registration (if self-registration enabled)
- On success: redirect to return URL or role-appropriate home

---

## Page: Join Event (`/join`)

**File:** `Pages/JoinEvent.razor`
**Auth:** Authenticated, not yet a participant
**Purpose:** Participant self-registration form.

**Content:**
- Form: first name, last name, work email, display name, team selector, leaderboard opt-in checkbox
- Validation errors inline
- Submit -> POST /api/events/{activeEventId}/participants
- On success: redirect to personal dashboard

---

## Page: Event Home (`/event/{eventId}`)

**File:** `Pages/EventHome.razor`
**Auth:** Authenticated
**Purpose:** Combined event overview and call-to-action hub.

**Content:**
- `EventProgressWidget` (full): total km, % complete, current virtual location, progress bar
- `MilestoneSummary` (next 3 milestones)
- `TeamLeaderboardPreview` (top 5 teams)
- `IndividualLeaderboardPreview` (top 5 participants)
- Participant: quick link to log a ride

---

## Page: Personal Dashboard (`/event/{eventId}/dashboard`)

**File:** `Pages/ParticipantDashboard.razor`
**Auth:** Participant, Admin
**Purpose:** Personal progress view.

**Stat Cards row:**
- My Total km
- My Team Total km
- Event Total km
- My Rank / Team Rank

**Sections:**
- `BadgeDisplay` component (earned badges)
- Next badge progress bar
- Next milestone km remaining
- `RecentActivityList` (last 5 rides with status chips)
- Quick "Log a Ride" button -> opens `ActivityForm` modal

---

## Page: My Activities (`/event/{eventId}/activities`)

**File:** `Pages/MyActivities.razor`
**Auth:** Participant
**Purpose:** Full activity list with add/edit/delete.

**Content:**
- Filter bar: date range, ride type, status
- `ActivityTable` (paginated):
  - Date, distance, ride type, source chip (Manual/Strava), status chip (Approved/Pending/Rejected)
  - Edit / Delete buttons (visible if activity is editable)
- "+ Log a Ride" button -> `ActivityFormModal`
- Strava connect widget (if event has stravaEnabled)

---

## Page: Leaderboards (`/event/{eventId}/leaderboard`)

**File:** `Pages/Leaderboards.razor`
**Auth:** All (respects event.leaderboardPublic)
**Purpose:** Individual and team rankings.

**Content:**
- Tab switcher: Individual | Team
- Individual tab: `IndividualLeaderboardTable` with rank, display name, team, total km, ride count
- Team tab: `TeamLeaderboardTable` with rank, team name, total km, active participants, avg km
- Pagination controls

---

## Page: Route & Milestones (`/event/{eventId}/milestones`)

**File:** `Pages/Milestones.razor`
**Auth:** All authenticated
**Purpose:** Route milestone progress view.

**Content:**
- Route progress bar (km achieved / total km)
- Current virtual location callout
- Timeline list of milestones:
  - Completed: checkmark, achieved date, km at achievement
  - Current/next: highlighted with "km remaining" badge
  - Future: greyed out
- Each milestone shows stop name, cumulative km, description, reward text if any

---

## Page: Edit My Profile (`/event/{eventId}/profile`)

**File:** `Pages/MyProfile.razor`
**Auth:** Participant (own)
**Purpose:** Update display name, team, leaderboard opt-in.

**Content:**
- Form: display name, team selector, leaderboard opt-in
- Strava connect/disconnect section (if enabled)
- Save button with success/error toast

---

## Page: Admin Hub (`/admin`)

**File:** `Pages/Admin/AdminHub.razor`
**Auth:** Admin
**Purpose:** Admin landing with quick-action tiles.

**Tiles:**
- Event Setup (link to `/admin/event`)
- Activities Review with pending count badge
- Duplicate Flags with count badge
- Reports
- Audit Log
- Participants
- Teams
- Milestones
- Badges

---

## Page: Admin Event Setup (`/admin/event`)

**File:** `Pages/Admin/AdminEventSetup.razor`
**Auth:** Admin
**Purpose:** Create and manage event configuration.

**Sections:**
- Event detail form: name, description, dates, route distance, manual entry mode, Strava enabled, banner, max single ride km, leaderboard public
- Status controls: Activate / Close / Archive / Revert to Draft buttons with confirmation dialogs
- Validation errors inline

---

## Page: Admin Milestone Manager (`/admin/milestones`)

**File:** `Pages/Admin/AdminMilestones.razor`
**Auth:** Admin
**Purpose:** CRUD milestones with ordering.

**Content:**
- Sortable table: order index, stop name, cumulative km, description, reward text
- Add / Edit / Delete buttons
- Drag-to-reorder (or up/down arrow buttons)
- Warning banner if event is Active (cumulative km locked)

---

## Page: Admin Team Manager (`/admin/teams`)

**File:** `Pages/Admin/AdminTeams.razor`
**Auth:** Admin
**Purpose:** Create and manage teams.

**Content:**
- Team list with participant count
- Add / Edit / Delete team buttons
- Set Captain dropdown per team

---

## Page: Admin Participant Manager (`/admin/participants`)

**File:** `Pages/Admin/AdminParticipants.razor`
**Auth:** Admin
**Purpose:** Manage participant registrations.

**Content:**
- Filterable / searchable participant table
- Columns: name, email, team, status, joined date, total km
- Add participant button
- Deactivate / Reactivate actions
- Change team action

---

## Page: Admin Activities Review (`/admin/activities`)

**File:** `Pages/Admin/AdminActivities.razor`
**Auth:** Admin
**Purpose:** Approval queue and full activity browser.

**Tabs:**
- Pending Approval queue (default): date, participant, distance, ride type, notes; Approve / Reject buttons
- All Activities: full filterable table with source, status, all columns

**Reject modal:** reason text input

---

## Page: Admin Duplicate Resolution (`/admin/duplicates`)

**File:** `Pages/Admin/AdminDuplicates.razor`
**Auth:** Admin
**Purpose:** Review and resolve flagged duplicate pairs.

**Content:**
- List of flagged pairs showing both activities side by side
- Resolve buttons: Keep Both / Keep First / Keep Second (invalidate other)
- Confirmation step before resolution

---

## Page: Admin Badge Manager (`/admin/badges`)

**File:** `Pages/Admin/AdminBadges.razor`
**Auth:** Admin
**Purpose:** Define and manage badge thresholds.

**Content:**
- Badge list with threshold, default/honorary indicator
- Add / Edit / Deactivate buttons
- Grant Honorary Badge form: select participant, select badge

---

## Page: Admin Reports (`/admin/reports`)

**File:** `Pages/Admin/AdminReports.razor`
**Auth:** Admin, ExecutiveViewer
**Purpose:** Trigger CSV exports.

**Sections per report:** filter options (date range, team, participant), Download CSV button, last generated timestamp

**Reports:**
- Participants
- Activities
- Team Totals
- Milestone Achievements
- Badge Awards
- Executive Summary

---

## Page: Audit Log (`/admin/audit`)

**File:** `Pages/Admin/AdminAuditLog.razor`
**Auth:** Admin
**Purpose:** Browse audit trail.

**Content:**
- Filter: entity type, entity id, actor, date range
- Paginated table: timestamp, actor, action, entity type, entity id
- Expandable row showing before/after JSON

---

## Page: Executive Summary (`/executive`)

**File:** `Pages/Executive/ExecutiveDashboard.razor`
**Auth:** ExecutiveViewer, Admin
**Purpose:** Read-only summary for leadership.

**Sections:**
- Headline metrics: total km, participants, teams, milestones reached
- Route progress card
- Team leaderboard (read-only top 10)
- Individual leaderboard (read-only top 10)
- Download Executive Summary CSV button

---

## Shared Components

```
Components/
  Shared/
    EventProgressWidget.razor      // Route km bar + location + %
    EventStatusBanner.razor        // Status chip banner (layout)
    StatCard.razor                 // Metric tile: label + value + icon
    RouteProgressBar.razor         // Horizontal km progress bar with milestone markers
    MilestoneSummary.razor         // Next 3 milestones with km remaining
    MilestoneTimeline.razor        // Full timeline list
    BadgeDisplay.razor             // Grid of earned badge chips
    NotificationBell.razor         // Icon with unread count badge
    NotificationList.razor         // Dropdown notification list
    Toast.razor                    // Success/error toast notification
    ConfirmDialog.razor            // Modal confirmation dialog
    LoadingSpinner.razor           // Spinner for async operations
    PaginationBar.razor            // Page navigation controls
    StatusChip.razor               // Colored chip: Approved/Pending/Rejected/etc

  Activities/
    ActivityTable.razor            // Sortable, filterable activity list
    ActivityFormModal.razor        // Create/edit activity modal
    ActivityStatusChip.razor       // Status + source combined chip

  Leaderboards/
    IndividualLeaderboardTable.razor
    TeamLeaderboardTable.razor
    LeaderboardRow.razor

  Admin/
    ApprovalCard.razor             // Single pending activity card
    DuplicatePairCard.razor        // Side-by-side duplicate comparison
```

---

## State Management

Use scoped services injected via DI for client-side state:

- `EventStateService` - caches active event, refreshes on login
- `ParticipantStateService` - caches own participant record and totals
- `NotificationStateService` - polls unread notification count (interval: 60s)
- `AuthStateProvider` - extends `AuthenticationStateProvider` for JWT claims

---

## HTTP Client Services (Blazor WASM)

```
Services/
  ApiClient.cs             // Base HttpClient wrapper with auth header injection
  EventService.cs
  MilestoneService.cs
  TeamService.cs
  ParticipantService.cs
  ActivityService.cs
  LeaderboardService.cs
  DashboardService.cs
  BadgeService.cs
  NotificationService.cs
  StravaService.cs
  ReportService.cs
```

Each service maps to the corresponding API controller and returns typed DTOs from `PedalAcrossCanada.Shared`.
