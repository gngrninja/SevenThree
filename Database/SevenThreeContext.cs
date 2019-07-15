using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace SevenThree.Database
{
    public class SevenThreeContext : DbContext
    {
        public DbSet<CallSignAssociation> CallSignAssociation { get; set; }
        public DbSet<Credentials> Credentials { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .AddJsonFile("config.json")
                .Build();
            optionsBuilder.UseSqlite($"Data Source={configuration["Db"]}");
        }
    }
}