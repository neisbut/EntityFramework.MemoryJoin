using System.Data.Entity;

namespace EntityFramework.MemoryJoin.TestRunner45.DAL
{
    public class SampleContext : DbContext
    {
        public DbSet<Address> Addresses { get; set; }

        protected DbSet<QueryModelClass> QueryData { get; set; }

        public SampleContext(string csName) : base(csName) { }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
        }
    }
}
