using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Core.Mapping;
using System.Data.Entity.Core.Metadata.Edm;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Reflection;

namespace EntityFramework.MemoryJoin.Internal
{
    /// <summary>
    /// Some internal helper methods for Npgsql
    /// </summary>
    internal static class EFHelper
    {

        internal static Dictionary<PropertyInfo, string> ConvertFragmentToMapping(
            DbContext context,
            Type type,
            MappingFragment mappingFragment,
            EntityType entityType)
        {

            var innerList = mappingFragment
                .PropertyMappings
                .OfType<ScalarPropertyMapping>()
                .ToDictionary(x => type.GetProperty(x.Property.Name,
                        BindingFlags.NonPublic | BindingFlags.Public |
                        BindingFlags.GetProperty | BindingFlags.Instance),
                    x => x.Column.Name);

            return innerList;
        }

        internal static EntitySetBase GetEntitySet(DbContext context, Type type)
        {
            var metadata = ((IObjectContextAdapter)context).ObjectContext.MetadataWorkspace;
            var baseTypeName = type.BaseType.Name;
            var typeName = type.Name;

            var es = metadata
                    .GetItemCollection(DataSpace.SSpace)
                    .GetItems<EntityContainer>()
                    .SelectMany(c => c.BaseEntitySets
                        .Where(e => e.Name == typeName || e.Name == baseTypeName))
                    .FirstOrDefault();

            return es;
        }

        internal static Dictionary<PropertyInfo, string> GetColumnNames(DbContext context, Type type)
        {
            var metadata = ((IObjectContextAdapter)context).ObjectContext.MetadataWorkspace;
            var objectItemCollection = ((ObjectItemCollection)metadata.GetItemCollection(DataSpace.OSpace));

            // Get the entity type from the model that maps to the CLR type
            var entityType = metadata.GetItems<EntityType>(DataSpace.OSpace)
                .Single(e => objectItemCollection.GetClrType(e) == type);

            var sets = metadata.GetItems<EntityContainer>(DataSpace.CSpace).Single().EntitySets;
            var entitySet = sets.SingleOrDefault(s => s.ElementType.Name == entityType.Name);

            var mappings = metadata.GetItems<EntityContainerMapping>(DataSpace.CSSpace).Single().EntitySetMappings;

            if (entitySet != null)
            {
                var mapping = mappings.Single(s => s.EntitySet == entitySet);
                var typeMappings = mapping.EntityTypeMappings;
                var mappingFragment = (typeMappings.Count == 1 ?
                    typeMappings.Single() :
                    typeMappings.Single(x => x.EntityType == null)).Fragments.Single();

                return ConvertFragmentToMapping(context, type, mappingFragment, entityType);
            }

            var partMapping = mappings
                .SelectMany(x => x.EntityTypeMappings)
                .Where(x => x.EntityType != null)
                .FirstOrDefault(x => x.EntityType.Name == type.Name);

            if (partMapping?.EntityType.BaseType == null) throw new NotSupportedException();

            var baseEntityType = metadata.GetItems<EntityType>(DataSpace.OSpace)
                .Single(e => e.Name == partMapping.EntityType.BaseType.Name);
            var baseClrType = objectItemCollection.GetClrType(baseEntityType);

            var baseTypeMapping = GetColumnNames(context, baseClrType);
            var subTypeMapping = ConvertFragmentToMapping(
                context, type, partMapping.Fragments.Single(), entityType);

            var union = baseTypeMapping.Union(subTypeMapping)
                .ToDictionary(x => x.Key, x => x.Value);
            return union;
        }

        internal static string GetTableName(DbContext context, Type t)
        {
            var entityType = GetEntitySet(context, t);
            return entityType.Table;
        }

        internal static string GetKeyProperty(DbContext context, Type t)
        {
            var entityType = GetEntitySet(context, t);

            if (entityType == null)
                throw new InvalidOperationException(
                    "QueryModelClass is not found in the context. Please check configuration");

            var kps = entityType.ElementType.KeyProperties;
            if (kps.Count > 1)
                throw new NotSupportedException("Multiple column PK is not supported");

            return kps.First().Name;
        }

    }
}
