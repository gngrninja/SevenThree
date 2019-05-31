using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace SevenThree.Database
{
    public class SevenThreeContext : DbContext
    {
        public SevenThreeContext(DbContextOptions<SevenThreeContext> options) 
            : base(options) 
            {
            }

        public DbSet<CallSignAssociation> CallSignAssociation { get; set; }

/* 
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Data Source=73.db");
        }
*/
    }
}