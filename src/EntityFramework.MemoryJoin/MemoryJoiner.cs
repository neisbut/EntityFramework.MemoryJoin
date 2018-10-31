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

        static MethodInfo takeMethod = typeof(Queryable)
            .GetTypeInfo()
            .GetMethods()
            .Where(x => x.Name == "Take")
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
        public static IQueryable<T> FromLocalList<T>(this DbContext context, IList<T> data, ValuesInjectionMethod method)
        {
            return FromLocalList<T>(context, data, typeof(QueryModelClass), method);
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
        public static IQueryable<T> FromLocalList<T>(this DbContext context, IList<T> data, Type queryClass)
        {
            return FromLocalList<T>(context, data, queryClass, ValuesInjectionMethod.Auto);
        }

        /// <summary>
        /// Returns queryable wrapper for data
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="data"></param>
        /// <returns></returns>
        public static IQueryable<T> FromLocalList<T>(this DbContext context, IList<T> data, Type queryClass, ValuesInjectionMethod method)
        {
            if (MemoryJoinerInterceptor.IsInterceptionEnabled(
                new[] { context }, out InterceptionOptions opts))
            {
                throw new InvalidOperationException(
                    "Only one data set can be applied to single DbContext before actuall DB request is done");
            }

            var propMapping = allowedMappingDict.GetOrAdd(queryClass, MappingHelper.GetPropertyMappings);
            var entityMapping = MappingHelper.GetEntityMapping<T>(context, queryClass, propMapping);
            var baseQuerySet = context.Set(queryClass);

            if (data.Any())
            {
                PrepareInjection(entityMapping, data, context, queryClass, method);

                var middleResult = selectMethod.MakeGenericMethod(queryClass, typeof(T))
                    .Invoke(null, new object[] { baseQuerySet, entityMapping.OutExpression });

                var querySet = (IQueryable<T>)middleResult;
                return querySet;
            }
            else
            {
                var emptyQueryable = takeMethod.MakeGenericMethod(queryClass).Invoke(null, new object[] { baseQuerySet, 0 });

                var middleResult = selectMethod.MakeGenericMethod(queryClass, typeof(T))
                    .Invoke(null, new object[] { emptyQueryable, entityMapping.OutExpression });

                var querySet = (IQueryable<T>)middleResult;
                return querySet;
            }
        }

        static void PrepareInjection<T>(
            Mapping<T> mapping,
            IList<T> data,
            DbContext context,
            Type queryClass,
            ValuesInjectionMethod method)
        {
            var opts = new InterceptionOptions
            {
                QueryTableName = EFHelper.GetTableName(context, queryClass),
                ColumnNames = mapping.UserProperties.Keys.ToArray(),
                Data = data
                    .Select(x => mapping.UserProperties.ToDictionary(y => y.Key, y => y.Value(x)))
                    .ToList(),
                ContextType = context.GetType(),
                ValuesInjectMethod = (ValuesInjectionMethodInternal)method,
                KeyColumnName = mapping.KeyColumnName
            };

            MemoryJoinerInterceptor.SetInterception(context, opts);
        }

    }
}
