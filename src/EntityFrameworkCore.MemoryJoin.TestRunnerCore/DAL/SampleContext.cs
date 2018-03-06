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
            modelBuilder.Entity<QueryModelClass>()
                .ToTable("__stub_query_data", "dbo");
            modelBuilder.Entity<Address>()
                .ToTable("addresses", "dbo");

            base.OnModelCreating(modelBuilder);
        }
    }
}
