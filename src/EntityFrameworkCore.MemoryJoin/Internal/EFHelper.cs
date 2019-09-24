using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace EntityFramework.MemoryJoin.Internal
{
    /// <summary>
    /// Some internal helper methods for EF
    /// </summary>
    internal static class EFHelper
    {
        internal static Dictionary<PropertyInfo, string> GetColumnNames(DbContext context, Type type)
        {
            var metadata = context.Model;
            var entityType = metadata.GetEntityTypes().Single(x => x.ClrType == type);


            var innerList = entityType
                .GetProperties()
                .Where(x => x.PropertyInfo != null)
                .ToDictionary(x => x.PropertyInfo, x => x.GetColumnName());
            
            return innerList;
        }

        internal static string GetTableName(DbContext context, Type t)
        {
            var relational = context.Model.FindEntityType(t);
            return relational.GetTableName();
        }

        internal static string GetKeyProperty(DbContext context, Type t)
        {
            var kps = context.Model.FindEntityType(t).FindPrimaryKey().Properties;
            if (kps.Count > 1)
                throw new NotSupportedException("Multiple column PK is not supported");

            return kps.First().GetColumnName();
        }

    }
}
