using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace SevenThree.Database
{
    public class SevenThreeContext : DbContext
    {
        public DbSet<CallSignAssociation> CallSignAssociation { get; set; }        
        public DbSet<HamTest> HamTest { get; set; }
        public DbSet<Questions> Questions { get; set; }
        public DbSet<Answer> Answer { get; set ;}
        public DbSet<Quiz> Quiz { get; set; }
        public DbSet<Figure> Figure { get; set; }
        public DbSet<UserAnswer> UserAnswer { get; set; }
        public DbSet<PrefixList> PrefixList { get; set; }
        public DbSet<Cred> Cred { get; set; }
        public DbSet<ApiData> ApiData { get; set; }
        public DbSet<QuizSettings> QuizSettings { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .AddJsonFile("config.json")
                .Build();
            optionsBuilder.UseSqlite($"Data Source={configuration["Db"]}");
        }
    }
}