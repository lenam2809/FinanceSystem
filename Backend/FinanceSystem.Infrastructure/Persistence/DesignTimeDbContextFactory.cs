using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace FinanceSystem.Infrastructure.Persistence;

/// <summary>
/// Design-time factory so EF tools can create the DbContext without running the API startup.
/// Reads connection string from environment or appsettings (tries API appsettings as fallback).
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var basePath = Directory.GetCurrentDirectory();

        // Try typical locations for appsettings.json: current folder or Backend/FinanceSystem.API
        var configBuilder = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile(Path.Combine("..", "Backend", "FinanceSystem.API", "appsettings.json"), optional: true)
            .AddEnvironmentVariables();

        var config = configBuilder.Build();

        var connectionString = config.GetConnectionString("DefaultConnection")
            ?? config["ConnectionStrings:DefaultConnection"]
            ?? "Host=localhost;Port=5432;Database=finance;Username=finance;Password=finance";

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new AppDbContext(optionsBuilder.Options);
    }
}
