using Microsoft.EntityFrameworkCore;
namespace SevenThree.Database
{
    public class SevenThreeContext : DbContext
    {
        public SevenThreeContext (DbContextOptions<SevenThreeContext> options)
            : base(options)
            {

            }
        public DbSet<CallSignAssociation> CallSignAssociation { get; set; }

    }
}