using Sentinel.Domain;

namespace Sentinel.Application;

public interface IRunbookService
{
    Task<List<Runbook>> GetAllAsync();
    Task<Runbook?> GetByIdAsync(Guid id);
    Task CreateAsync(Runbook runbook);
    Task UpdateAsync(Runbook runbook);
    Task DeleteAsync(Guid id);
}