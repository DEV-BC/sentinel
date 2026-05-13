using Sentinel.Domain;

namespace Sentinel.Application;

public interface IIncidentService
{
    Task<List<Incident>> GetAllAsync();
    Task<Incident?> GetByIdAsync(Guid id);
    Task CreateAsync(Incident incident);
    Task UpdateAsync(Incident incident);
    Task DeleteAsync(Guid id);

}