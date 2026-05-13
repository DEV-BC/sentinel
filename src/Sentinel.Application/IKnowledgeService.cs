using System.Security.Cryptography;
using Sentinel.Domain;

namespace Sentinel.Application;

public interface IKnowledgeService
{
    Task<List<KnowledgeEntry>> GetAllAsync();
    Task<KnowledgeEntry?> GetByIdAsync(Guid id);
    Task CreateAsync(KnowledgeEntry entry);
    Task UpdateAsync(KnowledgeEntry entry);
    Task DeleteAsync(Guid id);
    Task<List<KnowledgeEntry>> SearchAsync(float[] queryEmbedding, int limit = 5);
}