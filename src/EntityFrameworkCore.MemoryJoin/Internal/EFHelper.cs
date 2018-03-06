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
                .ToDictionary(x => x.PropertyInfo, x => x.Relational().ColumnName);

            return innerList;
        }

        internal static string GetTableName(DbContext context, Type t)
        {
            var relational = context.Model.FindEntityType(t).Relational();
            return relational.TableName;
        }

    }
}
