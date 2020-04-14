using EntityFrameworkCore.MemoryJoin.IntegrationTests.TestModels;
using EntityFrameworkCore.MemoryJoin.IntegrationTests.Utils;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EntityFrameworkCore.MemoryJoin.IntegrationTests
{
	[TestClass]
	public class SqliteTests
	{
		public TestContext TestContext { get; set; }

		[TestMethod]
		public async Task FromLocalList_ShouldReturnEmptyCollection_WhenJoinSourcesAreBothEmpty()
		{
			using (var context = CreateTestDbContext(nameof(FromLocalList_ShouldReturnEmptyCollection_WhenJoinSourcesAreBothEmpty)))
			{
				// test with both an empty in-memory collection and an empty real table
				var emptyCollection = new List<TestInMemoryEntity>(0);

				var inMemoryEntities = context.FromLocalList(emptyCollection);

				var joinedQueryable = context.Entities.Join(
					inMemoryEntities,
					inner => inner.Id,
					outer => outer.Id,
					(inner, outer) => new { Id = inner.Id, FromRealTable = inner.TestString, FromInMemoryTable = outer.Prop1 });

				var result = await joinedQueryable.ToListAsync();
				Assert.AreEqual(0, result.Count);
			}
		}

		[TestMethod]
		public async Task FromLocalList_ShouldReturnEmptyCollection_WhenInMemoryEntitiesIsEmpty()
		{
			using (var context = CreateTestDbContext(nameof(FromLocalList_ShouldReturnEmptyCollection_WhenInMemoryEntitiesIsEmpty)))
			{
				// test with an empty in-memory collection
				var emptyCollection = new List<TestInMemoryEntity>(0);
				var inMemoryEntities = context.FromLocalList(emptyCollection);

				// fill up some values in the real table
				context.AddRange(
					new TestEntity { Id = "1", TestInt = 1234, TestString = "abc" },
					new TestEntity { Id = "2", TestInt = 5678, TestString = "def" });
				await context.SaveChangesAsync();

				var joinedQueryable = context.Entities.Join(
					inMemoryEntities,
					inner => inner.Id,
					outer => outer.Id,
					(inner, outer) => new { Id = inner.Id, FromRealTable = inner.TestString, FromInMemoryTable = outer.Prop1 });

				var result = await joinedQueryable.ToListAsync();
				Assert.AreEqual(0, result.Count);
			}
		}

		[TestMethod]
		public async Task FromLocalList_ShouldReturnJoinedCollection_WhenSourcesContainAnIntersection()
		{
			using (var context = CreateTestDbContext(nameof(FromLocalList_ShouldReturnEmptyCollection_WhenInMemoryEntitiesIsEmpty)))
			{
				// fill up the in-memory table with 1 match
				var queryData = new List<TestInMemoryEntity> { new TestInMemoryEntity { Id = "1", Prop1 = 999 } };
				var inMemoryEntities = context.FromLocalList(queryData);

				// fill up some values in the real table
				context.AddRange(
					new TestEntity { Id = "1", TestInt = 1234, TestString = "abc" },
					new TestEntity { Id = "2", TestInt = 5678, TestString = "def" });
				await context.SaveChangesAsync();

				var joinedQueryable = context.Entities.Join(
					inMemoryEntities,
					inner => inner.Id,
					outer => outer.Id,
					(inner, outer) => new { Id = inner.Id, FromRealTable = inner.TestString, FromInMemoryTable = outer.Prop1 });

				var result = await joinedQueryable.ToListAsync();
				Assert.AreEqual(1, result.Count);
				Assert.AreEqual(new { Id = "1", FromRealTable = "abc", FromInMemoryTable = 999 }, result.First());
			}
		}

		private TestDbContext CreateTestDbContext(string databaseName)
		{
			var builder = new DbContextOptionsBuilder<TestDbContext>();

			// Create an in-memory DB for test purposes.
			var connection = new SqliteConnection("Data Source=:memory:;");
			connection.Open();
			builder = builder.UseSqlite(connection).EnableSensitiveDataLogging(true);

			if (TestContext != default)
			{
				builder.UseLoggerFactory(new TestLoggerFactory(() => TestContext));
			}

			var context = new TestDbContext(builder.Options);
			context.Database.EnsureCreated();

			return context;
		}
	}
}
