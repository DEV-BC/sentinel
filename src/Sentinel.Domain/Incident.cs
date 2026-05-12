namespace Sentinel.Domain;

public class Incident : BaseEntity
{
    public required string Title { get; set; }
    public string? Description { get; set; }
    public IncidentSeverity Severity { get; set; } = IncidentSeverity.Medium;
    public IncidentStatus Status { get; set; } = IncidentStatus.Open;
    public DateTime? ResolvedAt { get; set; }

    public Guid? RunbookId { get; set; }
    public Runbook? Runbook { get; set; }

}