using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ServiceRequests.Api.Data;

namespace ServiceRequests.Tests;

public sealed class ApiFactory(string environment = "Development") : WebApplicationFactory<Program>
{
    private readonly string database = Path.Combine(Path.GetTempPath(), $"service-requests-test-{Guid.NewGuid():N}.db");
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<RequestsDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<RequestsDbContext>>();
            services.AddDbContext<RequestsDbContext>(options => options.UseSqlite($"Data Source={database};Pooling=False"));
        });
    }
    public HttpClient User(string? user)
    {
        var client = CreateClient();
        using var scope = Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<RequestsDbContext>().Database.Migrate();
        if (user is not null) client.DefaultRequestHeaders.Add("X-Lab-User", user);
        return client;
    }
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;
        foreach (var suffix in new[] { "", "-wal", "-shm" }) File.Delete(database + suffix);
    }
}
