using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Pgvector;
using Sentinel.Domain;
using Sentinel.Infrastructure.Identity;

namespace Sentinel.Infrastructure;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Runbook> Runbooks => Set<Runbook>();
    public DbSet<Incident> Incidents => Set<Incident>();
    public DbSet<KnowledgeEntry> KnowledgeEntries => Set<KnowledgeEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasPostgresExtension("vector");
        modelBuilder.Entity<KnowledgeEntry>()
            .Property(e => e.Embedding)
            .HasConversion(v => new Vector(v!), v => v.ToArray(),
                    new ValueComparer<float[]>(
                            (a, b) => a!.SequenceEqual(b!),
                            a => a.Aggregate(0, (h, f) => HashCode.Combine(h, f.GetHashCode())),
                            a => a.ToArray()
                        )
                )
            .HasColumnType("vector(1536)");
    }
}
