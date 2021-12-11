# EntityFrameworkCore.MemoryJoin
Extension for EntityFramework for joins to in-memory data. Both Entity Framework 6 and Entity Framework Core are supported!
Used SQL standard syntax. 

Tested with: MSSQL, PostgreSQL, and SQLite. (others should also work as standard EF API and SQL standard syntax are used)

## Usage

1. Internally MemoryJoin uses intermediate class for making queries. So you can either use your own or basically use built-in one. Like this:

    ```protected DbSet<EntityFramework.MemoryJoin.QueryModelClass> QueryData { get; set; }```
    
    Or like this for EF Core
    
    ```protected DbSet<EntityFrameworkCore.MemoryJoin.QueryModelClass> QueryData { get; set; }```
    
  Please note this DbSet is protected, so it can't be used by anybody, only MemoryJoin will access it.
  Another note: table for QueryModelClass is NOT required. It is used for internal mapping only. So if you use migrations - basically use -IgnoreChanges flag.
  
  
  
2. After DbSet is defined you can write as follows:
  
    ```using EntityFramework.MemoryJoin```
    
    For EF Core:
    
    ```using EntityFrameworkCore.MemoryJoin```
    
    
  Then
  
    // get context
    var context = CreateContext();
    
    // define in-memory list
    var queryData = new [] {
        new { .StreetName = "Foo", .HouseNumber = 33 },
        new { .StreetName = "Baz", .HouseNumber = 99 },
        // can specify other objects here
    };
    
    // get queryable representation, using thing like AsQueryable() will not work
    var queryable = context.FromLocalList(queryData);
    
    // Write as complex query as you want now. Data will be sent to server for performing query. I.e.
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
       
       // Query will be executed on DB server
       var = efQuery.ToList();
