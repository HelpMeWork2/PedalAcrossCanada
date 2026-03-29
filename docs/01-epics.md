# 01 - Epics and Stories

## Epic 1: Event Foundation

**Goal:** Stand up the core data model, event lifecycle, milestones, participants, and teams.

### Stories

#### EVT-01-S1: Create Event (Admin)
- Admin can POST a new event with name, description, start date, end date, route distance km, manual entry mode, Strava enabled flag, and banner message
- Event is created in Draft status
- Validation: end date >= start date; name required

#### EVT-01-S2: List and View Events
- Admin can list all events
- Any authenticated user can view the active event
- Response includes event status, date range, and banner message

#### EVT-01-S3: Event Lifecycle Transitions
- Admin can transition: Draft -> Active, Active -> Closed, Closed -> Archived
- Only one Active event allowed at a time
- Cannot activate without at least one milestone
- Closed events are read-only to participants
- Archived events remain readable by admins

#### EVT-01-S4: Edit Event
- Admin can edit a Draft or Active event
- Status field changes use lifecycle endpoints, not direct edit
- All changes are audit logged

#### EVT-02-S1: Create and Order Milestones
- Admin can create milestones with stop name, order index, cumulative km, description, and optional reward text
- Milestone cumulative km must be ascending relative to adjacent milestones
- Admin can reorder milestones before activation

#### EVT-02-S2: View Route Progress
- Any authenticated user can view all milestones for an event
- API returns: completed milestones, current virtual location (nearest achieved), next milestone, km remaining

#### EVT-02-S3: Milestone Achievement
- When event total km crosses a milestone threshold, milestone is automatically marked achieved
- Achievement stores milestone id, achieved datetime UTC, total km at achievement
- Achievement triggers a Notification record (type: MilestoneReached)
- Triggered by: activity approval, Strava import

#### EVT-02-S4: Milestone Announcement
- Admin can mark an achieved milestone as Announced
- Stores announced-by and announced-date
- Milestone status: Pending -> ReadyForAnnouncement -> Announced

#### PAR-01-S1: Participant Self-Registration
- Authenticated user can register as participant in the active event
- Fields: first name, last name, work email, display name, team selection, leaderboard opt-in
- Prevents duplicate registration (same email + event)
- Creates participant with status Active and joined date UTC

#### PAR-01-S2: Admin Participant Management
- Admin can create participants manually
- Admin can deactivate a participant (status -> Inactive)
- Deactivated participants stay in reports
- Deactivation is audit logged

#### PAR-02-S1: Team CRUD (Admin)
- Admin can create, edit, delete teams for an event
- Team: name, description, optional captain (participant id)
- All team changes are audit logged

#### PAR-02-S2: Participant Team Assignment
- Participant can be assigned to one team per event
- Assignment change stores effective date in TeamHistory table
- Current leaderboard uses latest active team assignment

---

## Epic 2: Activity Tracking

**Goal:** Allow participants to log and manage rides; allow admins to approve or reject entries.

### Stories

#### ACT-01-S1: Create Manual Activity
- Participant can POST a manual activity: date, distance km, ride type, optional notes
- Validates: distance > 0 and <= max threshold; date within event window; not future-dated
- Tagged source = Manual
- Status defaults to Pending (approval required) or Approved (no approval required) based on event config

#### ACT-01-S2: Duplicate Warning
- On manual activity creation, system checks for same participant + same date + distance within 10% tolerance
- If duplicate candidate found, return 200 with `duplicateWarning: true` and `candidateActivityId`
- Client must re-submit with `acknowledgeDuplicate: true` to proceed

#### ACT-01-S3: Edit and Delete Manual Activity
- Participant can PUT their own manual activity while event is Active and activity status is Pending or Approved (not locked)
- Participant can DELETE their own manual Pending or Approved activity
- Edit/delete are audit logged
- Approved activities that are re-edited revert to Pending if approval is required

#### ACT-02-S1: Approval Queue
- Admin can GET paginated list of Pending activities
- Admin can approve an activity: status -> Approved, stores approvedBy + approvedAt
- Admin can reject: status -> Rejected, stores rejectionReason
- Approval/rejection triggers leaderboard recalculation and milestone check
- Approval/rejection is audit logged and creates Notification for participant

#### ACT-03-S1: Duplicate Detection and Resolution
- System flags suspected duplicates (same participant, same date, similar distance or same external id)
- Admin sees flagged duplicates in review queue
- Admin can: merge (keep one, invalidate other), keep both, or invalidate one
- Resolution is audit logged

---

## Epic 3: Strava Integration

**Goal:** Allow participants to optionally link Strava and auto-import rides.

### Stories

#### STRAVA-01-S1: Link Strava Account
- Participant initiates OAuth2 flow against Strava API
- Server handles callback, stores ExternalConnection: provider = Strava, externalAthleteId, encrypted token metadata, status = Connected, connectedAt
- Requires event.stravaEnabled = true
- Strava consent stored on Participant record

#### STRAVA-01-S2: Disconnect Strava
- Participant can disconnect
- Connection status -> Disconnected, disconnectedAt stored
- Historical imported activities preserved

#### STRAVA-02-S1: Manual Sync Trigger
- Connected participant can POST /api/strava/sync to trigger import
- System fetches activities from Strava within event date range of supported types (default: Ride)
- Converts meters to km (÷ 1000, round to 2 dp)
- Skips already-imported external activity ids
- Creates Activity records: source = Strava, status = Approved, countsTowardTotal = true

#### STRAVA-02-S2: Scheduled Background Sync (SYS-01)
- Background job runs on schedule (e.g. every 30 min) for all Connected participants in active events
- Job logs success/failure per participant
- Failed syncs create Notification (type: StravaSyncFailed) and are visible in admin dashboard
- Token expiry triggers refresh; if refresh fails, marks connection as RequiresReauth

#### STRAVA-02-S3: Admin Invalidate Imported Activity
- Admin can invalidate an imported activity with a reason
- Sets countsTowardTotal = false, status = Invalid
- Triggers leaderboard recalculation

#### SYS-01-S1: Leaderboard Refresh Job
- Background job recalculates all participant and team totals from approved/valid activities
- Runs on schedule or triggered after bulk operations
- Idempotent; failure does not corrupt existing totals

#### SYS-01-S2: Milestone Recalculation Job
- Background job rechecks all milestones for event against current total km
- Awards any milestones not yet marked achieved if threshold is now met
- Safe to re-run

---

## Epic 4: Engagement and Visibility

**Goal:** Deliver dashboards, leaderboards, badges, milestones display, and in-app notifications.

### Stories

#### LEAD-01-S1: Individual Leaderboard
- GET /api/events/{id}/leaderboards/individual
- Returns paginated, ranked list: rank, displayName, teamName, totalKm, rideCount
- Tie-breaking: most rides, then earliest join date, then same rank
- Respects event.leaderboardPublic setting

#### LEAD-02-S1: Team Leaderboard
- GET /api/events/{id}/leaderboards/team
- Returns ranked teams: rank, teamName, totalKm, activeParticipantCount, averageKmPerParticipant
- Admin configures whether to show average km

#### DASH-01-S1: Personal Dashboard
- GET /api/events/{id}/dashboard/me
- Returns: personalTotalKm, teamTotalKm, eventTotalKm, personalRank, teamRank, badgesEarned, nextBadgeThreshold, nextMilestoneKm, recentActivities (last 5)

#### DASH-02-S1: Event Progress Dashboard
- GET /api/events/{id}/dashboard
- Returns: totalEventKm, routeTotalKm, percentComplete, currentVirtualLocation, completedMilestones, nextMilestone, registeredParticipantCount, activeParticipantCount
- Admin view adds: pendingApprovals count, syncFailures count, duplicateFlags count

#### BADGE-01-S1: Badge Definitions
- Admin can CRUD badge definitions: name, description, thresholdKm (null = honorary/manual only)
- Seed default badges on first run

#### BADGE-01-S2: Automatic Badge Awards
- After any activity approval or import, system checks participant's total km against all badge thresholds
- Awards any newly crossed badges (no duplicates)
- Badge award creates Notification (type: BadgeEarned)

#### BADGE-01-S3: Manual Honorary Badge Grant
- Admin can grant any badge to any participant manually
- Stores awardedBy
- Audit logged

#### REWARD-01-S1: Milestone Reward Workflow
- Milestone achievement triggers status = ReadyForAnnouncement
- Admin sees milestone in announcement queue
- Admin marks as Announced with stored announcedBy + announcedDate

#### NOTIF-01-S1: In-App Notifications
- GET /api/notifications (authenticated, returns own notifications)
- PUT /api/notifications/{id}/read
- Notification types: MilestoneReached, BadgeEarned, ActivityRejected, StravaSyncFailed
- Admin notifications: PendingApprovals, DuplicateFlag, SyncFailure

---

## Epic 5: Admin, Security, Reporting

**Goal:** Secure the app, add role-based access, reporting, and audit trail.

### Stories

#### SEC-01-S1: Authentication Setup
- ASP.NET Core Identity with JWT bearer tokens
- Roles: Participant, TeamCaptain, Admin, ExecutiveViewer
- Login endpoint returns JWT; client stores in memory (not localStorage) for WASM security
- Refresh token support

#### SEC-01-S2: Role-Based Authorization
- All mutating endpoints require appropriate role
- Participant endpoints scoped to own data
- Admin endpoints protected with [Authorize(Roles = "Admin")]
- ExecutiveViewer endpoints return summary data only, no mutation

#### RPT-01-S1: CSV Exports
- GET /api/events/{id}/reports/participants -> CSV
- GET /api/events/{id}/reports/activities -> CSV (includes source, status)
- GET /api/events/{id}/reports/teams -> CSV
- GET /api/events/{id}/reports/milestones -> CSV
- GET /api/events/{id}/reports/badges -> CSV
- GET /api/events/{id}/reports/executive-summary -> CSV
- All include generated timestamp header
- Support query filters: startDate, endDate, teamId, participantId

#### AUDIT-01-S1: Audit Logging Middleware
- AuditLog entity: actor (userId), action, entityType, entityId, timestamp UTC, beforeJson, afterJson
- Logged for: event CRUD, milestone changes, participant activate/deactivate, activity CRUD, approvals, duplicate resolution, badge grants, Strava connect/disconnect
- Admin-only read endpoint: GET /api/audit?entityType=&entityId=&page=

#### API-01-S1: API Documentation
- Swagger/OpenAPI enabled in development
- All endpoints documented with summaries, request/response schemas
- Consistent error envelope: `{ "errors": { "field": ["message"] } }`
- Pagination envelope: `{ "data": [], "page": 1, "pageSize": 25, "totalCount": 0 }`
