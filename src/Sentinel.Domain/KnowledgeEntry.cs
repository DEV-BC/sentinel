namespace Sentinel.Domain;

public class KnowledgeEntry : BaseEntity
{
    public required string Title { get; set; }
    public required string Content { get; set; }
    public string? Source { get; set; }
    public float[]? Embedding { get; set; }
    
}