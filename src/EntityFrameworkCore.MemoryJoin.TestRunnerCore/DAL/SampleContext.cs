using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.MemoryJoin.TestRunnerCore.DAL
{
    public class SampleContext : DbContext
    {
        public DbSet<Address> Addresses { get; set; }

        protected DbSet<QueryModelClass> QueryData { get; set; }

        public SampleContext(DbContextOptions<SampleContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Address>().Property("StreetName");

            modelBuilder.Entity<QueryModelClass>().ToTable("betterName", "schema");

            base.OnModelCreating(modelBuilder);
        }
    }
}
