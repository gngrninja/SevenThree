using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.IO;
using SevenThree.Database;

namespace SevenThree.Services 
{
    public class DbService : IDesignTimeDbContextFactory<SevenThreeContext>
    {
        public SevenThreeContext CreateDbContext(string[] args)
        {
            var configurationBuilder = new ConfigurationBuilder()
              .SetBasePath(Directory.GetCurrentDirectory())
              .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

            IConfigurationRoot configuration = configurationBuilder.Build();
            string connectionString = configuration.GetConnectionString("SevenThreeContext");

            DbContextOptionsBuilder<SevenThreeContext> optionsBuilder = new DbContextOptionsBuilder<SevenThreeContext>()
                .UseSqlite(connectionString);

            
            return new SevenThreeContext(optionsBuilder.Options);
        }
    }
}