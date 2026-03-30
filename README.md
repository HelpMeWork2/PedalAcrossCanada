# Pedal Across Canada 🚴

An internal company web app that tracks employee cycling kilometres during a company-wide biking initiative. Participants log rides manually or via Strava sync, and progress is measured against a virtual **Montreal-to-Calgary route (~3,757 km)**. The app supports milestone celebrations, leaderboards, badges, and admin reporting.

## Features

- **Event management** — admins create challenge events with configurable date ranges, milestones, and rules
- **Participant registration** — employees self-register, join teams, and opt into leaderboards
- **Ride logging** — manual entry with duplicate detection, or automatic Strava import
- **Route milestones** — virtual stops along the Montreal → Calgary route, auto-achieved as cumulative km grows
- **Leaderboards** — individual and team rankings with tie-breaking rules
- **Badges** — automatic threshold-based awards (First Ride, 50 km Club, Century Rider, etc.)
- **In-app notifications** — milestone achievements, badge awards, activity rejections
- **Admin dashboard** — approval queues, reporting, CSV exports, and a full audit log
- **Executive view** — read-only summary dashboards for leadership

## Tech Stack

| Layer | Technology |
|---|---|
| Frontend | Blazor WebAssembly, .NET 10, Bootstrap 5 |
| Backend | ASP.NET Core Web API, .NET 10 |
| ORM | Entity Framework Core 10 |
| Database (dev) | SQLite |
| Auth | ASP.NET Core Identity + JWT Bearer |
| Background jobs | Hangfire |
| CSV export | CsvHelper |
| Client auth | Blazored.LocalStorage + custom `AuthenticationStateProvider` |

## Solution Structure

```
PedalAcrossCanada.sln
├── PedalAcrossCanada/             # Blazor WebAssembly client
├── PedalAcrossCanada.Server/      # ASP.NET Core Web API
└── PedalAcrossCanada.Shared/      # Shared DTOs and enums
```

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

## Getting Started

```bash
# Clone the repo
git clone https://github.com/HelpMeWork2/PedalAcrossCanada.git
cd PedalAcrossCanada

# Restore and build
dotnet build

# Run the server (applies migrations and seeds data on first start)
dotnet run --project PedalAcrossCanada.Server

# In a separate terminal, run the Blazor client
dotnet run --project PedalAcrossCanada
```

The server seeds default roles (Admin, Participant, TeamCaptain, ExecutiveViewer) and an initial admin user on first startup. Configure the admin credentials in `PedalAcrossCanada.Server/appsettings.Development.json`:

```json
{
  "AdminSeed": {
    "Email": "admin@example.com",
    "Password": "YourPassword1"
  }
}
```

## Implementation Progress

| Phase | Description | Status |
|---|---|---|
| 1 | Solution scaffold | ✅ Complete |
| 2 | Domain model + database | ✅ Complete |
| 3 | Authentication + Identity | ✅ Complete |
| 4 | Events + Milestones | ✅ Complete |
| 5 | Teams + Participants | 🔲 Not started |
| 6 | Manual activity entry + approval | 🔲 Not started |
| 7 | Totals, leaderboards, dashboards | 🔲 Not started |
| 8 | Badges + Notifications | 🔲 Not started |
| 9 | Reporting + Audit log | 🔲 Not started |
| 10 | Strava scaffold | 🔲 Not started |
| 11 | Strava import + background jobs | 🔲 Not started |
| 12 | Duplicate detection | 🔲 Not started |
| 13 | Executive view + polish | 🔲 Not started |

See [`docs/06-build-order.md`](docs/06-build-order.md) for the full phased build plan.

## Documentation

| Document | Description |
|---|---|
| [`docs/00-main-acceptance-criteria.md`](docs/00-main-acceptance-criteria.md) | Full acceptance criteria |
| [`docs/01-epics.md`](docs/01-epics.md) | Epic and story breakdown |
| [`docs/02-domain-model.md`](docs/02-domain-model.md) | Entity definitions and relationships |
| [`docs/03-api-outline.md`](docs/03-api-outline.md) | REST API endpoint list |
| [`docs/04-ui-pages.md`](docs/04-ui-pages.md) | Blazor page and component plan |
| [`docs/05-solution-architecture.md`](docs/05-solution-architecture.md) | Technical architecture decisions |
| [`docs/06-build-order.md`](docs/06-build-order.md) | Phased implementation plan |

## License

This project is for internal company use.
