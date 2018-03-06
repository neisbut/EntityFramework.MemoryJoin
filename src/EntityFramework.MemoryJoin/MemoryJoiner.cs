using EntityFramework.MemoryJoin.Internal;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure.Interception;
using System.Linq;
using System.Reflection;

namespace EntityFramework.MemoryJoin
{

    public static class MemoryJoiner
    {
        static ConcurrentDictionary<Type, Dictionary<Type, PropertyInfo[]>> allowedMappingDict =
            new ConcurrentDictionary<Type, Dictionary<Type, PropertyInfo[]>>();
        static MethodInfo selectMethod = typeof(Queryable).GetMethods()
            .Where(x => x.Name == "Select")
            .OrderBy(x => x.GetParameters().Length)
            .First();

        static MemoryJoiner()
        {
            DbInterception.Add(new MemoryJoinerInterceptor());
        }

        /// <summary>
        /// Returns queryable wrapper for data
        /// </summary>
        public static IQueryable<T> FromLocalList<T>(this DbContext context, IList<T> data)
        {
            return FromLocalList<T>(context, data, typeof(QueryModelClass));
        }

        /// <summary>
        /// Returns queryable wrapper for data
        /// </summary>
        public static IQueryable<T> FromLocalList<T, TQueryModel>(this DbContext context, IList<T> data)
        {
            return FromLocalList<T>(context, data, typeof(TQueryModel));
        }

        /// <summary>
        /// Returns queryable wrapper for data
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="data"></param>
        /// <returns></returns>
        public static IQueryable<T> FromLocalList<T>(this DbContext context, IList<T> data, Type queryClass)
        {
            if (MemoryJoinerInterceptor.IsInterceptionEnabled(
                new[] { context }, out InterceptionOptions opts))
            {
                throw new InvalidOperationException(
                    "Only one data set can be applied to single DbContext before actuall DB request is done");
            }

            var propMapping = allowedMappingDict.GetOrAdd(queryClass, (t) => MappingHelper.GetPropertyMappings(t));
            var entityMapping = MappingHelper.GetEntityMapping<T>(context, queryClass, propMapping);

            PrepareInjection(entityMapping.UserProperties, data, context, queryClass);

            var baseQuerySet = context.Set(queryClass);

            var middleResult = selectMethod.MakeGenericMethod(queryClass, typeof(T))
                .Invoke(null, new object[] { baseQuerySet, entityMapping.OutExpression });

            var querySet = (IQueryable<T>)middleResult;
            return querySet;
        }

        static void PrepareInjection<T>(
            Dictionary<string, Func<T, object>> usedProperties,
            IList<T> data,
            DbContext context,
            Type queryClass)
        {
            var opts = new InterceptionOptions
            {
                QueryTableName = EFHelper.GetTableName(context, queryClass),
                ColumnNames = usedProperties.Keys.ToArray(),
                Data = data
                    .Select(x => usedProperties.ToDictionary(y => y.Key, y => y.Value(x)))
                    .ToList(),
                ContextType = context.GetType()
            };

            MemoryJoinerInterceptor.SetInterception(context, opts);
        }

    }
}
