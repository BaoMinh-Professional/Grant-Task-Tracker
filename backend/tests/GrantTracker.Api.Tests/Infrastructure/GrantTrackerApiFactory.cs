using GrantTracker.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace GrantTracker.Api.Tests.Infrastructure;

public sealed class GrantTrackerApiFactory : WebApplicationFactory<Program>
{
    private readonly string databaseName = $"api-tests-{Guid.NewGuid():N}";
    private readonly SqliteConnection keepAlive;

    public GrantTrackerApiFactory()
    {
        keepAlive = new SqliteConnection($"Data Source={databaseName};Mode=Memory;Cache=Shared");
        keepAlive.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:GrantTracker"] = $"Data Source={databaseName};Mode=Memory;Cache=Shared",
            ["LogFile:Path"] = ""
        }));
        builder.ConfigureTestServices(services =>
        {
            services.AddControllers().AddApplicationPart(typeof(GrantTrackerApiFactory).Assembly);
        });
    }

    public HttpClient CreateClient(Guid? userId = null)
    {
        var client = base.CreateClient();
        if (userId.HasValue) client.DefaultRequestHeaders.Add("X-User-Id", userId.Value.ToString());
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) keepAlive.Dispose();
    }
}
