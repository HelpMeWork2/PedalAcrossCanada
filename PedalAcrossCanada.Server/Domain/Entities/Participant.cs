using PedalAcrossCanada.Shared.Enums;

namespace PedalAcrossCanada.Server.Domain.Entities;

public class Participant
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string WorkEmail { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public Guid? TeamId { get; set; }
    public ParticipantStatus Status { get; set; }
    public DateTime JoinedAt { get; set; }
    public bool LeaderboardOptIn { get; set; }
    public bool StravaConsentGiven { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Event Event { get; set; } = null!;
    public Team? Team { get; set; }
    public ICollection<Activity> Activities { get; set; } = [];
    public ICollection<BadgeAward> BadgeAwards { get; set; } = [];
    public ICollection<ExternalConnection> ExternalConnections { get; set; } = [];
    public ICollection<Notification> Notifications { get; set; } = [];
    public ICollection<TeamHistory> TeamHistories { get; set; } = [];
}
