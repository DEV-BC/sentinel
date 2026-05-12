namespace Sentinel.Domain;

public class Runbook : BaseEntity
{
    public required string Title { get; set; }
    public required string Content { get; set; }
    public string? Description { get; set; }
    public string Author { get; set; } = string.Empty;
    public RunbookStatus Status { get; set; } = RunbookStatus.Draft;
}