namespace PedalAcrossCanada.Shared.DTOs.Audit;

public class AuditLogDto
{
    public Guid Id { get; set; }
    public string Actor { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string? BeforeSummary { get; set; }
    public string? AfterSummary { get; set; }
    public Guid? EventId { get; set; }
}
