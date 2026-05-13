using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using Sentinel.Application;
using Sentinel.Domain;

namespace Sentinel.Infrastructure.Services;

public class KnowledgeService : IKnowledgeService
{
    private readonly AppDbContext _db;
  
    public KnowledgeService(AppDbContext db) => _db = db;

    public async Task<List<KnowledgeEntry>> GetAllAsync() =>
        await _db.KnowledgeEntries.OrderByDescending(k => k.CreatedAt).ToListAsync();

    public async Task<KnowledgeEntry?> GetByIdAsync(Guid id) =>
        await _db.KnowledgeEntries.FindAsync(id);

    public async Task CreateAsync(KnowledgeEntry entry)
    {
        _db.KnowledgeEntries.Add(entry);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(KnowledgeEntry entry)
    {
        entry.UpdatedAt = DateTime.UtcNow;
        _db.KnowledgeEntries.Update(entry);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var entry = await _db.KnowledgeEntries.FindAsync(id);
        if (entry is not null)
        {
            _db.KnowledgeEntries.Remove(entry);
            await _db.SaveChangesAsync();
        }
    }
    
    public async Task<List<KnowledgeEntry>> SearchAsync(float[] queryEmbedding, int limit = 5)
    {
        var vector = new Vector(queryEmbedding);
        return await _db.KnowledgeEntries
            .Where(e => e.Embedding != null)
            .Where(e => e.Embedding!.CosineDistance(vector) < 0.7)
            .OrderBy(e => e.Embedding!.CosineDistance(vector))
            .Take(limit)
            .ToListAsync();

    }       


}