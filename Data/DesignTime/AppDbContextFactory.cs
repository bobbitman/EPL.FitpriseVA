using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace FitpriseVA.Data.DesignTime
{
    public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            // Build config pointing at the project root so appsettings*.json are found
            var basePath = Directory.GetCurrentDirectory();

            var cfg = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile("appsettings.Development.json", optional: true) // used when running locally
                .AddEnvironmentVariables()
                .Build();

            var conn = cfg.GetConnectionString("DefaultConnection")
                       ?? "Server=.;Database=FitpriseVADb;Trusted_Connection=True;TrustServerCertificate=True;";

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(conn)
                .Options;

            return new AppDbContext(options);
        }
    }
}
