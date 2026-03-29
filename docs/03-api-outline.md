# 03 - API Outline

Base path: `/api`
Auth: JWT Bearer (issued by the Server project via ASP.NET Core Identity)
Content-Type: `application/json`

## Standard Response Envelopes

```json
// Success (paginated)
{
  "data": [],
  "page": 1,
  "pageSize": 25,
  "totalCount": 100
}

// Validation Error (400)
{
  "errors": {
    "distanceKm": ["Distance must be greater than 0."],
    "activityDate": ["Activity date must be within the event date range."]
  }
}

// Generic Error (4xx/5xx)
{
  "title": "Forbidden",
  "status": 403,
  "detail": "You do not have permission to perform this action."
}
```

---

## Auth Endpoints

| Method | Path | Roles | Description |
|---|---|---|---|
| POST | /api/auth/login | Anonymous | Login; returns `{ accessToken, refreshToken, expiresAt }` |
| POST | /api/auth/refresh | Anonymous | Refresh token |
| POST | /api/auth/logout | Authenticated | Invalidate refresh token |
| POST | /api/auth/register | Anonymous | Create account (self-registration) |
| GET | /api/auth/me | Authenticated | Current user info and roles |

---

## Events

| Method | Path | Roles | Description |
|---|---|---|---|
| GET | /api/events | Admin, ExecutiveViewer | List all events (paginated) |
| POST | /api/events | Admin | Create event |
| GET | /api/events/{eventId} | All | Get event detail |
| PUT | /api/events/{eventId} | Admin | Update event (non-status fields) |
| POST | /api/events/{eventId}/activate | Admin | Transition Draft -> Active |
| POST | /api/events/{eventId}/close | Admin | Transition Active -> Closed |
| POST | /api/events/{eventId}/archive | Admin | Transition Closed -> Archived |
| POST | /api/events/{eventId}/revert-to-draft | Admin | Revert Active -> Draft (if no activities) |

---

## Milestones

| Method | Path | Roles | Description |
|---|---|---|---|
| GET | /api/events/{eventId}/milestones | All | List milestones with achievement status |
| POST | /api/events/{eventId}/milestones | Admin | Create milestone |
| PUT | /api/events/{eventId}/milestones/{id} | Admin | Edit milestone |
| DELETE | /api/events/{eventId}/milestones/{id} | Admin | Delete milestone (Draft only) |
| POST | /api/events/{eventId}/milestones/{id}/announce | Admin | Mark achieved milestone as Announced |

---

## Teams

| Method | Path | Roles | Description |
|---|---|---|---|
| GET | /api/events/{eventId}/teams | All | List teams |
| POST | /api/events/{eventId}/teams | Admin | Create team |
| GET | /api/events/{eventId}/teams/{id} | All | Get team |
| PUT | /api/events/{eventId}/teams/{id} | Admin | Update team |
| DELETE | /api/events/{eventId}/teams/{id} | Admin | Delete team (no members) |
| POST | /api/events/{eventId}/teams/{id}/set-captain | Admin | Set team captain |

---

## Participants

| Method | Path | Roles | Description |
|---|---|---|---|
| GET | /api/events/{eventId}/participants | Admin, TeamCaptain | List participants (paginated) |
| POST | /api/events/{eventId}/participants | Authenticated | Self-register as participant |
| GET | /api/events/{eventId}/participants/{id} | Admin, self | Get participant detail |
| PUT | /api/events/{eventId}/participants/{id} | Admin, self | Update profile |
| POST | /api/events/{eventId}/participants/{id}/deactivate | Admin | Deactivate participant |
| POST | /api/events/{eventId}/participants/{id}/change-team | Admin | Change participant's team |
| GET | /api/events/{eventId}/participants/me | Authenticated | Shortcut: get own participant record |

---

## Activities

| Method | Path | Roles | Description |
|---|---|---|---|
| GET | /api/events/{eventId}/activities | Admin | List all activities (paginated, filterable) |
| GET | /api/events/{eventId}/participants/{participantId}/activities | Admin, self | List participant's activities |
| POST | /api/events/{eventId}/activities | Participant | Create manual activity |
| GET | /api/events/{eventId}/activities/{id} | Admin, owner | Get activity |
| PUT | /api/events/{eventId}/activities/{id} | Owner (if unlocked) | Edit manual activity |
| DELETE | /api/events/{eventId}/activities/{id} | Owner (if unlocked) | Delete manual activity |
| POST | /api/events/{eventId}/activities/{id}/approve | Admin | Approve pending activity |
| POST | /api/events/{eventId}/activities/{id}/reject | Admin | Reject activity |
| POST | /api/events/{eventId}/activities/{id}/invalidate | Admin | Invalidate imported or approved activity |
| POST | /api/events/{eventId}/activities/{id}/lock | Admin | Prevent participant editing |

**Query parameters for list:**
- `page`, `pageSize`
- `status` (Pending, Approved, Rejected, Invalid)
- `source` (Manual, Strava)
- `teamId`
- `participantId`
- `startDate`, `endDate`
- `duplicateFlagged` (bool)

---

## Duplicate Detection

| Method | Path | Roles | Description |
|---|---|---|---|
| GET | /api/events/{eventId}/activities/duplicates | Admin | List flagged duplicate pairs |
| POST | /api/events/{eventId}/activities/duplicates/{id}/resolve | Admin | Resolve: `{ resolution: "KeepBoth" / "KeepFirst" / "KeepSecond" }` |

---

## Leaderboards

| Method | Path | Roles | Description |
|---|---|---|---|
| GET | /api/events/{eventId}/leaderboards/individual | All (if public) | Individual rankings (paginated) |
| GET | /api/events/{eventId}/leaderboards/team | All | Team rankings |

**Query parameters:**
- `page`, `pageSize`

---

## Dashboards

| Method | Path | Roles | Description |
|---|---|---|---|
| GET | /api/events/{eventId}/dashboard | All | Event-wide progress dashboard |
| GET | /api/events/{eventId}/dashboard/me | Participant | Personal progress dashboard |
| GET | /api/events/{eventId}/dashboard/admin | Admin | Admin dashboard with queues |

---

## Badges

| Method | Path | Roles | Description |
|---|---|---|---|
| GET | /api/badges | All | List active badge definitions |
| POST | /api/badges | Admin | Create badge definition |
| PUT | /api/badges/{id} | Admin | Update badge definition |
| DELETE | /api/badges/{id} | Admin | Deactivate badge |
| GET | /api/events/{eventId}/participants/{participantId}/badges | Admin, self | List earned badges |
| POST | /api/events/{eventId}/participants/{participantId}/badges | Admin | Grant honorary badge |

---

## Notifications

| Method | Path | Roles | Description |
|---|---|---|---|
| GET | /api/notifications | Authenticated | Get own notifications (paginated) |
| GET | /api/notifications/unread-count | Authenticated | Get unread count |
| PUT | /api/notifications/{id}/read | Authenticated | Mark notification read |
| PUT | /api/notifications/read-all | Authenticated | Mark all read |

---

## Strava

| Method | Path | Roles | Description |
|---|---|---|---|
| GET | /api/strava/auth-url | Participant | Get Strava OAuth authorization URL |
| GET | /api/strava/callback | Anonymous | OAuth callback (redirected from Strava) |
| POST | /api/strava/disconnect | Participant | Disconnect Strava |
| POST | /api/strava/sync | Participant | Trigger manual sync |
| GET | /api/strava/status | Participant | Get connection status and last sync |

---

## Reports (CSV Downloads)

All report endpoints require Admin or ExecutiveViewer role.
Response: `text/csv` with `Content-Disposition: attachment; filename="..."`

| Method | Path | Description |
|---|---|---|
| GET | /api/events/{eventId}/reports/participants | Participant list CSV |
| GET | /api/events/{eventId}/reports/activities | Activity detail CSV |
| GET | /api/events/{eventId}/reports/teams | Team totals CSV |
| GET | /api/events/{eventId}/reports/milestones | Milestone achievement CSV |
| GET | /api/events/{eventId}/reports/badges | Badge award CSV |
| GET | /api/events/{eventId}/reports/executive-summary | Executive summary CSV |

**Shared query parameters:**
- `startDate`, `endDate`
- `teamId`
- `participantId`

---

## Audit Log

| Method | Path | Roles | Description |
|---|---|---|---|
| GET | /api/audit | Admin | Query audit log (paginated) |

**Query parameters:**
- `entityType`, `entityId`
- `actor`
- `startDate`, `endDate`
- `page`, `pageSize`

---

## Background Job Endpoints (Internal / Admin)

| Method | Path | Roles | Description |
|---|---|---|---|
| POST | /api/jobs/recalculate-totals | Admin | Trigger full leaderboard recalculation |
| POST | /api/jobs/recalculate-milestones | Admin | Trigger milestone recalculation |
| POST | /api/jobs/strava-sync-all | Admin | Trigger sync for all connected participants |

---

## Controller Structure (Server Project)

```
Controllers/
  AuthController.cs
  EventsController.cs
  MilestonesController.cs
  TeamsController.cs
  ParticipantsController.cs
  ActivitiesController.cs
  DuplicatesController.cs
  LeaderboardsController.cs
  DashboardsController.cs
  BadgesController.cs
  NotificationsController.cs
  StravaController.cs
  ReportsController.cs
  AuditController.cs
  JobsController.cs
```
