using Microsoft.EntityFrameworkCore;
using Sentinel.Application;
using Sentinel.Domain;

namespace Sentinel.Infrastructure.Services;

public class IncidentService : IIncidentService
{
    private readonly AppDbContext _db;

    public IncidentService(AppDbContext db) => _db = db;

    public async Task<List<Incident>> GetAllAsync() =>
        await _db.Incidents.OrderByDescending(i => i.CreatedAt).ToListAsync();
    
    public async Task<Incident?> GetByIdAsync(Guid id) =>
        await _db.Incidents.FindAsync(id);

    public async Task CreateAsync(Incident incident)
    {
        _db.Incidents.Add(incident);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(Incident incident)
    {
        incident.UpdatedAt = DateTime.UtcNow;
        _db.Incidents.Update(incident);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var incident = await _db.Incidents.FindAsync(id);
        if (incident is not null)
        {
            _db.Incidents.Remove(incident);
            await _db.SaveChangesAsync();
        }
    }

}