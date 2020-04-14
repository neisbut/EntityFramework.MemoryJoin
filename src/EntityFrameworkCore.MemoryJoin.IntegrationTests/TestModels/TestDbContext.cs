namespace EntityFrameworkCore.MemoryJoin.IntegrationTests.TestModels
{
    using Microsoft.EntityFrameworkCore;

    public class TestDbContext : DbContext
    {
        public TestDbContext(DbContextOptions<TestDbContext> options)
            : base(options)
        {
        }

        public DbSet<TestEntity> Entities { get; set; }

        protected DbSet<QueryModelClass> QueryData { get; set; }
    }
}
