using EntityFrameworkCore.MemoryJoin.TestRunnerCore.DAL;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace EntityFrameworkCore.MemoryJoin.TestRunner45
{
    class Program
    {
        static void Main(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<SampleContext>();
            //optionsBuilder.UseNpgsql("server=localhost;user id=postgres;password=qwerty;database=copy");
            optionsBuilder.UseSqlServer("Data Source=localhost;Initial Catalog=copy;Integrated Security=True;Pooling=False");
            //optionsBuilder.UseLoggerFactory(
            //    new LoggerFactory(new[] { new ConsoleLoggerProvider((_, __) => true, true) }));

            var context = new SampleContext(optionsBuilder.Options);
            context.Database.EnsureCreated();

            FillTestData(context);

            for (var count = 100; count <= 1000; count += 100)
            {
                var localList = GetTestAddressData(count).
                Select(x => new
                {
                    x.StreetName,
                    x.HouseNumber,
                    DateTime = DateTime.Now,
                    Extra = "I'm from \n ' \" local!",
                    Integer = 123,
                    Float = 321.0f
                })
                .ToList();

                var queryList = context.FromLocalList(localList);

                var efQuery = from addr in context.Addresses
                              join el in queryList on
                                new { addr.StreetName, addr.HouseNumber } equals
                                new { el.StreetName, el.HouseNumber }
                              select new
                              {
                                  addr.AddressId,
                                  addr.CreatedAt,
                                  addr.StreetName,
                                  addr.HouseNumber,
                                  el.Extra,
                                  el.Integer,
                                  el.Float,
                                  el.DateTime
                              };

                var sw = Stopwatch.StartNew();
                var result = efQuery.ToList();
                sw.Stop();
                Console.WriteLine($"Success, requested: {count}, retrieved: {result.Count} elements in {sw.Elapsed}");
            }
            Console.ReadLine();
        }

        static List<Address> GetTestAddressData(int count)
        {
            var streets = new[] { "First", "Second", "Third" };
            var codes = new[] { "001001", "002002", "003003", "004004" };
            var extraNumbers = new int?[] { null, 1, 2, 3, 5, 8, 13, 21, 34 };

            var data = Enumerable.Range(0, count)
                .Select((x, i) => new Address()
                {
                    StreetName = streets[i % streets.Length],
                    HouseNumber = i + 1,
                    PostalCode = codes[i % codes.Length],
                    ExtraHouseNumber = extraNumbers[i % extraNumbers.Length],
                    CreatedAt = DateTime.Now
                }).ToList();

            return data;
        }

        static void FillTestData(SampleContext context)
        {
            if (context.Addresses.Any())
                return;

            var data = GetTestAddressData(100000);

            context.Addresses.AddRange(data);
            context.SaveChanges();
        }

    }
}
