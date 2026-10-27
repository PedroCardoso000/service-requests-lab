using Microsoft.EntityFrameworkCore;
using ServiceRequests.Api.Domain;

namespace ServiceRequests.Api.Data;

public sealed class RequestsDbContext(DbContextOptions<RequestsDbContext> options) : DbContext(options)
{
    public DbSet<ServiceRequest> Requests => Set<ServiceRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ServiceRequest>();
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
        entity.Property(x => x.Description).HasMaxLength(5000).IsRequired();
        entity.Property(x => x.RequesterId).IsRequired();
        entity.Property(x => x.CreatedAt).HasConversion(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
        entity.Property(x => x.Category).HasConversion<string>();
        entity.Property(x => x.Priority).HasConversion<string>();
        entity.Property(x => x.Status).HasConversion<string>();
        entity.Property(x => x.Version).IsConcurrencyToken();
        entity.HasIndex(x => x.RequesterId);
        entity.HasIndex(x => new { x.Status, x.OperatorId });
    }
}
