using EntityFramework.MemoryJoin.TestRunner45.DAL;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;

namespace EntityFramework.MemoryJoin.TestRunner45
{
    class Program
    {
        static void Main(string[] args)
        {

#if NETSTANDARD
            DbProviderFactories.RegisterFactory("Npgsql", Npgsql.NpgsqlFactory.Instance);
#endif

            var context = new SampleContext("DefaultConnection");
            FillTestData(context);

            for (var count = 1000; count <= 10000; count += 1000)
            {
                var localList = GetTestAddressData(count).
                    Select(x => new
                    {
                        x.StreetName,
                        x.HouseNumber,
                        Extra = "I'm from local!",
                        Integer = ((long)int.MaxValue) + 20,
                        Float = 321.0f,
                        Date = DateTime.Now,
                        Guid = Guid.NewGuid(),
                        Bool = true
                    })
                    .ToList();

                var queryList = context.FromLocalList(localList, ValuesInjectionMethod.ViaSqlQueryBody);
                // var queryList2 = context.GetQueryable(localList);

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
                                  el.Float
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
                    AddressGuid = Guid.NewGuid(),
                    AddressBool = i % 1 == 1
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
