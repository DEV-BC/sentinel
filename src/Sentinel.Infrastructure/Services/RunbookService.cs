using Microsoft.EntityFrameworkCore;
using Sentinel.Application;
using Sentinel.Domain;

namespace Sentinel.Infrastructure.Services;

public class RunbookService : IRunbookService
{
    private readonly AppDbContext _db;
  
    public RunbookService(AppDbContext db) => _db = db;

    public async Task<List<Runbook>> GetAllAsync() =>
        await _db.Runbooks.OrderByDescending(r => r.CreatedAt).ToListAsync();

    public async Task<Runbook?> GetByIdAsync(Guid id) =>
        await _db.Runbooks.FindAsync(id);

    public async Task CreateAsync(Runbook runbook)
    {
        _db.Runbooks.Add(runbook);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(Runbook runbook)
    {
        runbook.UpdatedAt = DateTime.UtcNow;
        _db.Runbooks.Update(runbook);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var runbook = await _db.Runbooks.FindAsync(id);
        if (runbook is not null)
        {
            _db.Runbooks.Remove(runbook);
            await _db.SaveChangesAsync();
        }
    }

}