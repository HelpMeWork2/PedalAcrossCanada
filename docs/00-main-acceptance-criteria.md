# 00 - Main Acceptance Criteria

## Feature Pack: Company Cycling Challenge Tracker

**Product Name:** Cycle Challenge Tracker
**Purpose:** Track employee cycling kilometres during a company-wide biking initiative, aggregate progress toward a shared virtual destination, support milestones, leaderboards, and rewards, and optionally integrate with Strava.

## Business Goal

Create a lightweight internal app that:

* tracks kilometres travelled by each participant
* calculates cumulative team progress
* maps progress against a virtual route from Montreal to Calgary
* supports milestone recognition and awards
* provides visibility to participants, admins, and leadership
* optionally syncs activities from Strava while still allowing manual entry where needed

## MVP Scope

MVP must include:

1. Participant registration and profile management
2. Event setup and date range management
3. Manual kilometre entry
4. Optional Strava account linking scaffold
5. Participant totals and team totals
6. Route milestone tracking
7. Individual and team leaderboards
8. Admin reporting and export
9. Basic badge / milestone achievement support
10. Teams/department grouping
11. Basic audit logging

## Out of Scope for MVP

* Payroll or HRIS integration
* Native mobile app
* Advanced gamification engine
* Route map animation
* Merchandise ordering
* Donations / charity payment flows
* Complex approval workflow for every ride
* Full wellness suite

## Primary Personas

* **Participant**: employee entering or syncing rides
* **Team Captain**: optional department/team lead reviewing team participation
* **Admin**: manages event, milestones, users, and reports
* **Executive Viewer**: sees progress dashboards and summary views only

## Success Measures

* % of registered participants with at least 1 activity
* total kilometres logged
* % of kilometres synced automatically vs entered manually
* number of milestones reached
* number of active teams participating
* number of repeat weekly participants
* admin time needed to run the challenge

## Core Rules

* Only rides within the configured event date range count toward the event
* Distance must be stored in kilometres to 2 decimal places
* Duplicate activity detection must exist
* Manual entries may require admin review if configured
* Strava-imported activities must be marked as imported
* Each participant can belong to one primary team for leaderboard purposes in MVP
* Milestones are awarded based on cumulative event team distance
* Individual badges may also be awarded based on personal thresholds

---

## EVT-01: Event Setup

### Goal
Allow admins to create and manage a cycling challenge event.

### Acceptance Criteria

* Admin can create an event with:
  * event name
  * description
  * start date
  * end date
  * route total distance in km
  * event status: Draft, Active, Closed, Archived
* Admin can define whether manual entry is:
  * allowed without approval
  * allowed with approval
  * disabled
* Admin can define whether Strava sync is enabled
* Admin can set a public event banner / intro message
* Admin can only have one Active event at a time in MVP
* System prevents end date earlier than start date
* System prevents activation of an event without at least one milestone
* Closed events become read-only to participants
* Archived events remain reportable by admins

---

## EVT-02: Route and Milestones

### Goal
Represent the virtual Montreal-to-Calgary route and milestone stops.

### Acceptance Criteria

* Admin can configure route stops with:
  * stop name
  * order index
  * cumulative distance km
  * description
  * optional reward text
* System validates milestone distances are ascending
* System displays:
  * completed milestones
  * current position on route
  * next milestone
  * km remaining to next milestone
* When cumulative event distance crosses a milestone, system marks it achieved
* Milestone achievement stores:
  * milestone id
  * achieved date/time
  * total km at achievement
* Admin can edit milestones before event activation
* After activation, admin can only edit milestone description/reward text, not cumulative km, unless event is placed back into Draft
* System supports at least 50 milestones in MVP

---

## PAR-01: Participant Registration

### Goal
Allow employees to join the event.

### Acceptance Criteria

* Participant can register with:
  * first name
  * last name
  * work email
  * display name
  * department/team
* System prevents duplicate participant creation for same active event and email
* Participant can opt into leaderboard display using display name
* Participant can join only once per event
* Admin can add participants manually
* Admin can deactivate a participant
* Deactivated participants remain in historical reports
* Participant profile stores:
  * status: Active, Inactive
  * joined date
  * consent to sync Strava data if applicable

---

## PAR-02: Team / Department Management

### Goal
Group participants into teams for reporting and competition.

### Acceptance Criteria

* Admin can create teams/departments
* Participant can be assigned to one team in MVP
* Team can have:
  * team name
  * description
  * optional captain
* Team leaderboard shows:
  * total km
  * average km per active participant
  * participant count
* Team changes are audit logged
* If participant changes team mid-event, system:
  * stores effective date
  * uses latest active team for current leaderboard in MVP
  * preserves historical activity ownership to participant

---

## ACT-01: Manual Activity Entry

### Goal
Allow participants to log rides manually.

### Acceptance Criteria

* Participant can create manual ride entry with:
  * activity date
  * distance km
  * ride type: commute, leisure, training, other
  * optional notes
* System validates:
  * distance > 0
  * distance <= configured max threshold per entry
  * activity date within event date range
* System prevents future-dated entries
* System warns on possible duplicates based on same user + same date + similar distance
* Manual entries are tagged as `manual`
* If admin approval is enabled:
  * entry status defaults to Pending
  * only Approved entries count toward totals
* If admin approval is not enabled:
  * entry status defaults to Approved
* Participant can edit or delete their own manual entries until event close unless locked by admin
* All edits are audit logged

---

## ACT-02: Activity Approval

### Goal
Allow admins to review manual entries when approval is required.

### Acceptance Criteria

* Admin sees queue of Pending activities
* Admin can approve, reject, or request correction
* Admin can add rejection reason / comment
* Participant can see rejection reason
* Approved entries immediately update totals and milestone calculations
* Rejected entries do not count toward any totals
* System stores approval metadata:
  * approved by
  * approved date
  * rejection reason if applicable

---

## STRAVA-01: Strava Account Linking Scaffold

### Goal
Support optional Strava integration for participants.

### Acceptance Criteria

* Participant can initiate Strava connection
* System stores:
  * external athlete id
  * token metadata
  * connection status
  * last sync date/time
* Participant can disconnect Strava
* Disconnected accounts stop future syncs but preserve historical imported activities
* System clearly explains which data will be imported
* If Strava integration is disabled for the event, linking UI is hidden
* System supports feature flagging so Strava can be turned on later without redesign

---

## STRAVA-02: Strava Activity Import

### Goal
Import eligible ride data from Strava.

### Acceptance Criteria

* System imports only supported activity types configured by admin, default `Ride`
* System imports only activities whose activity date falls within the active event date range
* Imported activity stores:
  * external activity id
  * activity date/time
  * distance km
  * source = Strava
  * ride title if available
* System prevents duplicate imports of same external activity id
* Imported activities automatically count toward totals unless marked invalid
* Admin can invalidate imported activity with reason
* Invalidated activities no longer count toward totals
* Participant can trigger manual sync
* System also supports scheduled background sync
* Sync errors are logged and visible to admin
* Token expiration / reauthorization states are handled gracefully

---

## ACT-03: Duplicate Detection

### Goal
Reduce double counting from manual and imported data.

### Acceptance Criteria

* System checks for duplicates across:
  * same participant
  * same source activity id
  * same date and similar distance
* Potential duplicates are flagged for review
* Admin can merge, keep both, or invalidate one
* Participant cannot create a manual entry matching an already imported exact activity without warning
* Duplicate handling decisions are audit logged

---

## LEAD-01: Individual Leaderboard

### Goal
Show participant rankings.

### Acceptance Criteria

* Leaderboard ranks active participants by approved/imported event km descending
* System supports tie handling by:
  1. greater ride count
  2. earlier join date
  3. same rank if still tied
* Participant sees:
  * rank
  * display name
  * team
  * total km
  * ride count
* Admin can toggle whether full leaderboard is visible to all participants
* Executive viewer sees summary leaderboard without edit controls

---

## LEAD-02: Team Leaderboard

### Goal
Show team performance.

### Acceptance Criteria

* Team leaderboard ranks teams by total km descending
* Team card shows:
  * team name
  * total km
  * active participants
  * average km per active participant
* Admin can choose whether leaderboard shows total km only or total + average
* Teams with no activity still display if configured by admin

---

## DASH-01: Participant Dashboard

### Goal
Give each participant a personal view of their progress.

### Acceptance Criteria

* Participant dashboard shows:
  * personal total km
  * team total km
  * event total km
  * current team rank
  * personal rank
  * personal badges earned
  * next personal badge threshold
  * next team milestone
* Participant can view recent activities
* Participant can see whether rides are manual/imported/pending/rejected
* Participant sees km remaining until next personal and team goal

---

## DASH-02: Event Progress Dashboard

### Goal
Show whole-event progress toward Calgary.

### Acceptance Criteria

* Dashboard shows:
  * total event km
  * route total km
  * percent complete
  * current virtual location
  * completed milestones
  * next milestone
  * number of registered participants
  * number of active participants
* Admin dashboard includes:
  * pending approvals
  * sync failures
  * entries flagged as duplicates
* Executive view presents summary metrics without admin actions

---

## BADGE-01: Badge Engine

### Goal
Award badges for participation and distance achievements.

### Acceptance Criteria

* Admin can define personal badge thresholds by cumulative km
* System supports default badges such as:
  * First Ride
  * 50 km
  * 100 km
  * 250 km
  * 500 km
* Badge award is automatic when threshold is crossed
* Badge award stores:
  * participant id
  * badge id
  * awarded date
* Participant can view earned badges
* Badge awards are not duplicated
* Admin can manually grant honorary badges

---

## REWARD-01: Milestone Reward Tracking

### Goal
Track milestone-based awards and communications.

### Acceptance Criteria

* Admin can attach reward text and celebration note to each milestone
* When milestone is reached, admin sees milestone marked Ready for Announcement
* Admin can mark milestone as Announced
* System stores:
  * announced by
  * announced date
* System supports export of milestone achievement history
* MVP does not need prize inventory management

---

## RPT-01: Reporting and Export

### Goal
Allow admins to export usable reporting data.

### Acceptance Criteria

* Admin can export CSV for:
  * participants
  * activities
  * team totals
  * milestone achievements
  * badge awards
* Export respects selected event
* Activity export includes source and approval status
* Reports include generated timestamp
* Admin can filter reports by date range, team, and participant
* Executive summary export includes headline metrics and leaderboard snapshot

---

## NOTIF-01: Notifications

### Goal
Support lightweight engagement notifications.

### Acceptance Criteria

* System can generate notification records for:
  * milestone reached
  * badge earned
  * activity rejected
  * Strava sync failed
* Participant can view in-app notifications
* Admin can view system notifications requiring action
* MVP may defer email sending, but notification model must support future email expansion

---

## AUDIT-01: Audit Logging

### Goal
Track important system changes.

### Acceptance Criteria

* System audit logs:
  * event changes
  * milestone changes
  * participant activation/deactivation
  * activity create/edit/delete
  * approval/rejection actions
  * duplicate resolution
  * manual badge grants
  * Strava connect/disconnect
* Audit entry stores:
  * actor
  * action
  * entity type
  * entity id
  * timestamp
  * before/after summary where applicable
* Audit logs are admin-only

---

## SEC-01: Authentication and Authorization

### Goal
Protect user data and role-based actions.

### Acceptance Criteria

* System supports authenticated access
* Roles:
  * Participant
  * TeamCaptain
  * Admin
  * ExecutiveViewer
* Participant can only edit own profile and own activities
* TeamCaptain can view own team summary only if enabled
* Admin has full CRUD over event configuration and moderation
* ExecutiveViewer has read-only summary access
* Unauthorized actions return proper error responses and are logged

---

## VAL-01: Validation Rules

### Goal
Ensure trustworthy data.

### Acceptance Criteria

* Distance precision stored to 2 decimal places
* Date must be valid and within event window
* System prevents null required fields
* Maximum single ride threshold configurable by admin
* Imported distance conversion from meters to km is standardized
* All leaderboard totals use only approved/valid activities

---

## SYS-01: Background Jobs

### Goal
Support scheduled operations.

### Acceptance Criteria

* System supports scheduled jobs for:
  * Strava sync
  * leaderboard refresh
  * milestone recalculation
* Jobs log success/failure
* Failed jobs do not corrupt totals
* Recalculation job can rebuild totals from source activities if needed

---

## UI-01: Basic Pages

### Goal
Define MVP page set.

### Acceptance Criteria

* MVP pages include:
  * Login
  * Event Home
  * Participant Dashboard
  * Leaderboards
  * My Activities
  * Join Event / Profile
  * Admin Event Setup
  * Admin Activities Review
  * Admin Reports
  * Milestones
* Navigation is role-aware
* Event status banner is visible on all event pages

---

## API-01: Core API Requirements

### Goal
Support a clean API-first backend.

### Acceptance Criteria

* API endpoints exist for:
  * events
  * milestones
  * participants
  * teams
  * activities
  * approvals
  * leaderboards
  * badges
  * reports
  * Strava connect/sync callbacks
* All mutating endpoints require auth and role checks
* API returns consistent validation errors
* API supports pagination for activities and leaderboards
* API contracts are documented

---

## DATA-01: Core Domain Entities

### Goal
Define minimum data structure.

### Acceptance Criteria

* Domain entities include:
  * Event
  * Milestone
  * Participant
  * Team
  * Activity
  * Badge
  * BadgeAward
  * AuditLog
  * Notification
  * ExternalConnection
* Activity must include source, status, and counting flag
* Historical records are retained for reporting even after event close

---

## NFR-01: Non-Functional Requirements

### Goal
Ensure the MVP is usable and supportable.

### Acceptance Criteria

* Dashboard loads in acceptable time for at least 1,000 participants and 50,000 activities
* Recalculation job completes successfully for same volume
* System uses UTC storage with user-friendly display formatting
* Sensitive tokens are encrypted at rest
* App supports modern desktop browsers
* All critical actions have user-visible success/error feedback
* System is recoverable by rebuilding leaderboard totals from activities

---

## Suggested Initial Route Milestones (Seed Data)

| # | Stop Name | Cumulative Km (approx) |
|---|-----------|----------------------|
| 1 | Montreal | 0 |
| 2 | Ottawa | 198 |
| 3 | Kingston | 362 |
| 4 | Peterborough | 448 |
| 5 | Oshawa | 504 |
| 6 | Toronto | 543 |
| 7 | Hamilton | 594 |
| 8 | Guelph | 625 |
| 9 | Waterloo | 652 |
| 10 | Barrie | 737 |
| 11 | Sudbury | 996 |
| 12 | Elliot Lake | 1076 |
| 13 | Sault Ste. Marie | 1192 |
| 14 | Marathon | 1506 |
| 15 | Thunder Bay | 1713 |
| 16 | Dryden | 2016 |
| 17 | Kenora | 2115 |
| 18 | Winnipeg | 2294 |
| 19 | Brandon | 2472 |
| 20 | Regina | 2773 |
| 21 | Moose Jaw | 2836 |
| 22 | Saskatoon | 3028 |
| 23 | Lloydminster | 3224 |
| 24 | Edmonton | 3508 |
| 25 | Calgary | 3757 |
