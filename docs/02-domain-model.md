# 02 - Domain Model

All entities live in `PedalAcrossCanada.Server` under `Domain/Entities/`.
All dates are stored as `DateTime` in UTC. EF Core value converters ensure UTC on read.
All decimal distances use `decimal` with precision `(10, 2)`.

---

## Enumerations

```csharp
// Domain/Enums/EventStatus.cs
public enum EventStatus { Draft, Active, Closed, Archived }

// Domain/Enums/ManualEntryMode.cs
public enum ManualEntryMode { AllowedWithoutApproval, AllowedWithApproval, Disabled }

// Domain/Enums/ActivitySource.cs
public enum ActivitySource { Manual, Strava }

// Domain/Enums/ActivityStatus.cs
public enum ActivityStatus { Pending, Approved, Rejected, Invalid }

// Domain/Enums/RideType.cs
public enum RideType { Commute, Leisure, Training, Other }

// Domain/Enums/ParticipantStatus.cs
public enum ParticipantStatus { Active, Inactive }

// Domain/Enums/ConnectionStatus.cs
public enum ConnectionStatus { Connected, Disconnected, RequiresReauth }

// Domain/Enums/AnnouncementStatus.cs
public enum AnnouncementStatus { Pending, ReadyForAnnouncement, Announced }

// Domain/Enums/NotificationType.cs
public enum NotificationType { MilestoneReached, BadgeEarned, ActivityRejected, StravaSyncFailed, PendingApproval, DuplicateFlag }
```

---

## Entity: Event

**Table:** `Events`

| Column | Type | Notes |
|---|---|---|
| Id | Guid | PK |
| Name | string(200) | Required |
| Description | string(2000) | |
| StartDate | DateTime UTC | Required |
| EndDate | DateTime UTC | Required; >= StartDate |
| RouteDistanceKm | decimal(10,2) | Total route length |
| Status | EventStatus | Default: Draft |
| ManualEntryMode | ManualEntryMode | Default: AllowedWithApproval |
| StravaEnabled | bool | Default: false |
| BannerMessage | string(1000) | |
| MaxSingleRideKm | decimal(10,2) | Configurable threshold; default 300 |
| LeaderboardPublic | bool | Default: true |
| ShowTeamAverage | bool | Default: true |
| CreatedAt | DateTime UTC | |
| UpdatedAt | DateTime UTC | |

**Navigation:** Milestones, Teams, Participants, Activities

**Business Rules (enforced in service layer):**
- Only one `Active` event at a time
- Cannot activate without at least one Milestone
- EndDate >= StartDate

---

## Entity: Milestone

**Table:** `Milestones`

| Column | Type | Notes |
|---|---|---|
| Id | Guid | PK |
| EventId | Guid | FK -> Events |
| StopName | string(200) | Required |
| OrderIndex | int | Required; unique per event |
| CumulativeDistanceKm | decimal(10,2) | Must be ascending within event |
| Description | string(1000) | |
| RewardText | string(500) | |
| AchievedAt | DateTime? UTC | Null until reached |
| TotalKmAtAchievement | decimal? | |
| AnnouncementStatus | AnnouncementStatus | Default: Pending |
| AnnouncedBy | string? | UserId of admin |
| AnnouncedAt | DateTime? UTC | |

**Business Rules:**
- CumulativeDistanceKm must be strictly ascending within an event
- CumulativeDistanceKm and OrderIndex immutable after event activation (unless reverted to Draft)

---

## Entity: Team

**Table:** `Teams`

| Column | Type | Notes |
|---|---|---|
| Id | Guid | PK |
| EventId | Guid | FK -> Events |
| Name | string(200) | Required; unique per event |
| Description | string(500) | |
| CaptainParticipantId | Guid? | FK -> Participants; nullable |
| CreatedAt | DateTime UTC | |
| UpdatedAt | DateTime UTC | |

**Navigation:** Participants, TeamHistory

---

## Entity: Participant

**Table:** `Participants`

| Column | Type | Notes |
|---|---|---|
| Id | Guid | PK |
| EventId | Guid | FK -> Events |
| UserId | string | ASP.NET Identity user id |
| FirstName | string(100) | Required |
| LastName | string(100) | Required |
| WorkEmail | string(256) | Required; unique per event |
| DisplayName | string(100) | Required |
| TeamId | Guid? | FK -> Teams (current team) |
| Status | ParticipantStatus | Default: Active |
| JoinedAt | DateTime UTC | |
| LeaderboardOptIn | bool | Default: true |
| StravaConsentGiven | bool | Default: false |
| CreatedAt | DateTime UTC | |
| UpdatedAt | DateTime UTC | |

**Unique constraint:** (EventId, WorkEmail)

**Navigation:** Activities, BadgeAwards, Notifications, ExternalConnections, TeamHistory

---

## Entity: TeamHistory

**Table:** `TeamHistory`

| Column | Type | Notes |
|---|---|---|
| Id | Guid | PK |
| ParticipantId | Guid | FK -> Participants |
| TeamId | Guid | FK -> Teams |
| EffectiveFrom | DateTime UTC | |

Used to track team changes. Current team = latest record by EffectiveFrom.

---

## Entity: Activity

**Table:** `Activities`

| Column | Type | Notes |
|---|---|---|
| Id | Guid | PK |
| ParticipantId | Guid | FK -> Participants |
| EventId | Guid | FK -> Events |
| ActivityDate | DateTime UTC | Date only (time stored as midnight UTC) |
| DistanceKm | decimal(10,2) | > 0; <= Event.MaxSingleRideKm |
| RideType | RideType | Required |
| Notes | string(500) | |
| Source | ActivitySource | Manual or Strava |
| Status | ActivityStatus | Pending / Approved / Rejected / Invalid |
| CountsTowardTotal | bool | True if Approved and not Invalid |
| ExternalActivityId | string? | Strava activity id; unique per participant |
| ExternalTitle | string? | Strava ride name |
| ImportedAt | DateTime? UTC | When Strava import ran |
| ApprovedBy | string? | UserId |
| ApprovedAt | DateTime? UTC | |
| RejectedBy | string? | UserId |
| RejectedAt | DateTime? UTC | |
| RejectionReason | string(500)? | |
| IsDuplicateFlagged | bool | Default: false |
| DuplicateOfActivityId | Guid? | FK -> Activities (self) |
| LockedByAdmin | bool | Default: false |
| CreatedAt | DateTime UTC | |
| UpdatedAt | DateTime UTC | |

**Unique constraint:** (ParticipantId, ExternalActivityId) where ExternalActivityId is not null

**Computed property (not persisted):**
`CountsTowardTotal` = Status == Approved && !IsDuplicateFlagged (unless admin resolved)

---

## Entity: Badge

**Table:** `Badges`

| Column | Type | Notes |
|---|---|---|
| Id | Guid | PK |
| Name | string(100) | Required |
| Description | string(500) | |
| ThresholdKm | decimal? | Null = honorary/manual only |
| IsDefault | bool | Seeded on startup |
| IsActive | bool | Default: true |
| SortOrder | int | Display order |

**Seed data:**
| Name | ThresholdKm |
|---|---|
| First Ride | 0.01 (any positive distance) |
| 50 km Club | 50 |
| Century Rider | 100 |
| Quarter Crusher | 250 |
| 500 km Legend | 500 |

---

## Entity: BadgeAward

**Table:** `BadgeAwards`

| Column | Type | Notes |
|---|---|---|
| Id | Guid | PK |
| ParticipantId | Guid | FK -> Participants |
| BadgeId | Guid | FK -> Badges |
| EventId | Guid | FK -> Events |
| AwardedAt | DateTime UTC | |
| AwardedBy | string? | UserId if manual; null if automatic |
| IsManual | bool | |

**Unique constraint:** (ParticipantId, BadgeId, EventId)

---

## Entity: ExternalConnection

**Table:** `ExternalConnections`

| Column | Type | Notes |
|---|---|---|
| Id | Guid | PK |
| ParticipantId | Guid | FK -> Participants |
| Provider | string(50) | e.g. "Strava" |
| ExternalAthleteId | string(100) | Provider-assigned athlete id |
| EncryptedTokenData | string | AES-256 encrypted JSON blob containing access_token, refresh_token, expires_at |
| ConnectionStatus | ConnectionStatus | |
| LastSyncAt | DateTime? UTC | |
| ConnectedAt | DateTime UTC | |
| DisconnectedAt | DateTime? UTC | |

**Unique constraint:** (ParticipantId, Provider)

---

## Entity: AuditLog

**Table:** `AuditLogs`

| Column | Type | Notes |
|---|---|---|
| Id | Guid | PK |
| Actor | string | UserId or "system" |
| Action | string(100) | e.g. "ActivityApproved", "ParticipantDeactivated" |
| EntityType | string(100) | e.g. "Activity", "Participant" |
| EntityId | string | Guid as string |
| Timestamp | DateTime UTC | |
| BeforeSummary | string? | JSON snapshot before change |
| AfterSummary | string? | JSON snapshot after change |
| EventId | Guid? | FK -> Events for context |

Index on (EntityType, EntityId) and (Actor) and (Timestamp desc).

---

## Entity: Notification

**Table:** `Notifications`

| Column | Type | Notes |
|---|---|---|
| Id | Guid | PK |
| ParticipantId | Guid? | FK -> Participants; null = system/admin notification |
| NotificationType | NotificationType | |
| Title | string(200) | |
| Message | string(1000) | |
| RelatedEntityType | string? | e.g. "Milestone", "Badge" |
| RelatedEntityId | string? | Guid as string |
| IsRead | bool | Default: false |
| CreatedAt | DateTime UTC | |
| EmailSent | bool | Default: false (reserved for future) |
| EmailSentAt | DateTime? UTC | |

---

## DbContext Structure

```
Infrastructure/
  Data/
    AppDbContext.cs
    Configurations/
      EventConfiguration.cs
      MilestoneConfiguration.cs
      TeamConfiguration.cs
      ParticipantConfiguration.cs
      TeamHistoryConfiguration.cs
      ActivityConfiguration.cs
      BadgeConfiguration.cs
      BadgeAwardConfiguration.cs
      ExternalConnectionConfiguration.cs
      AuditLogConfiguration.cs
      NotificationConfiguration.cs
    Migrations/
    Seed/
      BadgeSeedData.cs
      MilestoneSeedData.cs
```

---

## Entity Relationship Summary

```
Event
  ├─ Milestones (1:N)
  ├─ Teams (1:N)
  └─ Participants (1:N)
       ├─ TeamHistory (1:N)
       ├─ Activities (1:N)
       │    └─ self-ref DuplicateOf (0..1:N)
       ├─ BadgeAwards (1:N) -> Badge
       ├─ ExternalConnections (1:N)
       └─ Notifications (1:N)

Badge (global, not event-scoped)

AuditLog (global, event-contextual via EventId)
```
