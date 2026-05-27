using Microsoft.EntityFrameworkCore;

namespace Kotirauha.Infrastructure;

public class KotirauhaDbContext : DbContext
{
    public KotirauhaDbContext(DbContextOptions<KotirauhaDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(KotirauhaDbContext).Assembly);
    }
}
