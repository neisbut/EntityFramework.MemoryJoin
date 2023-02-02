using EntityFramework.MemoryJoin.Internal;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Text;

namespace EntityFrameworkCore.MemoryJoin
{

    public static class MemoryJoiner
    {
        /// <summary>
        /// Parameter names prefix which is used in SQL expression
        /// </summary>
        public static string ParametersPrefix { get; set; } = "@__gen_q_p_";

        static MethodInfo selectMethod = typeof(Queryable)
            .GetTypeInfo()
            .GetMethods()
            .Where(x => x.Name == "Select")
            .OrderBy(x => x.GetParameters().Length)
            .First();

        static MethodInfo setMethod = typeof(DbContext).GetMethod("Set", new Type[] { });

        static Type rawSqlStringType = typeof(RelationalQueryableExtensions).Assembly.GetType("Microsoft.EntityFrameworkCore.RawSqlString");

        static MethodInfo fromSqlMethod = typeof(RelationalQueryableExtensions)
            .GetTypeInfo()
            .GetMethods()
            .Where(x => x.Name == "FromSql")
            .OrderByDescending(x => x.GetParameters().Length)
            .FirstOrDefault();

        static MethodInfo fromSqlRawMethod = typeof(RelationalQueryableExtensions)
            .GetTypeInfo()
            .GetMethods()
            .Where(x => x.Name == "FromSqlRaw")
            .OrderByDescending(x => x.GetParameters().Length)
            .FirstOrDefault();

        static ConcurrentDictionary<Type, Dictionary<Type, PropertyInfo[]>> allowedMappingDict =
            new ConcurrentDictionary<Type, Dictionary<Type, PropertyInfo[]>>();

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

        ///// <summary>
        ///// Returns queryable wrapper for data
        ///// </summary>
        ///// <typeparam name="T"></typeparam>
        ///// <param name="data"></param>
        ///// <returns></returns>
        //public static IQueryable<T> FromLocalList2<T>(this DbContext context, IList<T> data, Type queryClass, ValuesInjectionMethod method)
        //{
        //    var sb = new StringBuilder(100);

        //    var propMapping = allowedMappingDict.GetOrAdd(queryClass, (t) => MappingHelper.GetPropertyMappings(t));
        //    var entityMapping = MappingHelper.GetEntityMapping<T>(context, queryClass, propMapping);

        //    var opts = new InterceptionOptions
        //    {
        //        QueryTableName = EFHelper.GetTableName(context, queryClass),
        //        ColumnNames = entityMapping.UserProperties.Keys.ToArray(),
        //        Data = data
        //            .Select(x => entityMapping.UserProperties.ToDictionary(y => y.Key, y => y.Value(x)))
        //            .ToList(),
        //        ContextType = context.GetType(),
        //        ValuesInjectMethod = (ValuesInjectionMethodInternal)method,
        //        KeyColumnName = entityMapping.KeyColumnName
        //    };

        //    var connection = context.Database.GetDbConnection();
        //    using (var command = connection.CreateCommand())
        //    {
        //        var parameters = new List<DbParameter>();
        //        MappingHelper.ComposeTableSql(
        //            sb,
        //            opts,
        //            command,
        //            parameters);

        //        var set = setMethod.MakeGenericMethod(queryClass)
        //            .Invoke(context, new object[] { });

        //        var rawSqlString = new RawSqlString(sb.ToString());

        //        var fromSql = fromSqlMethod
        //            .MakeGenericMethod(queryClass)
        //            .Invoke(null, new object[] { set, rawSqlString, parameters.ToArray() });

        //        var middleResult = selectMethod.MakeGenericMethod(queryClass, typeof(T))
        //            .Invoke(null, new object[] { fromSql, entityMapping.OutExpression });

        //        var querySet = (IQueryable<T>)middleResult;
        //        return querySet;
        //    }
        //}

        public static IQueryable<T> FromLocalList<T>(this DbContext context, IList<T> data, Type queryClass, ValuesInjectionMethod method)
        {
            var sb = new StringBuilder(100);

            var propMapping = allowedMappingDict.GetOrAdd(queryClass, (t) => MappingHelper.GetPropertyMappings(t));
            var entityMapping = MappingHelper.GetEntityMapping<T>(context, queryClass, propMapping);

            var opts = new InterceptionOptions
            {
                QueryTableName = EFHelper.GetTableName(context, queryClass),
                ColumnNames = entityMapping.UserProperties.Keys.ToArray(),
                Data = data
                    .Select(x => entityMapping.UserProperties.ToDictionary(y => y.Key, y => y.Value(x)))
                    .ToList(),
                ContextType = context.GetType(),
                ValuesInjectMethod = (ValuesInjectionMethodInternal)method,
                KeyColumnName = entityMapping.KeyColumnName
            };

            var connection = context.Database.GetDbConnection();
            using (var command = connection.CreateCommand())
            {
                var parameters = new List<DbParameter>();
                MappingHelper.ComposeTableSql(
                    sb,
                    opts,
                    command,
                    parameters);

                var set = setMethod.MakeGenericMethod(queryClass)
                    .Invoke(context, new object[] { });

                if (fromSqlMethod != null && rawSqlStringType != null)
                {
                    // .Net Core < 5 case
                    var rawSqlString = Activator.CreateInstance(rawSqlStringType, new object[] { sb.ToString() });

                    var fromSql = fromSqlMethod
                        .MakeGenericMethod(queryClass)
                        .Invoke(null, new object[] { set, rawSqlString, parameters.ToArray() });

                    var middleResult = selectMethod.MakeGenericMethod(queryClass, typeof(T))
                        .Invoke(null, new object[] { fromSql, entityMapping.OutExpression });

                    var querySet = (IQueryable<T>)middleResult;
                    return querySet;
                }
                else
                {
                    // .Net 5 case
                    var fromSql = fromSqlRawMethod
                        .MakeGenericMethod(queryClass)
                        .Invoke(null, new object[] { set, sb.ToString(), parameters.ToArray() });

                    var middleResult = selectMethod.MakeGenericMethod(queryClass, typeof(T))
                        .Invoke(null, new object[] { fromSql, entityMapping.OutExpression });

                    var querySet = (IQueryable<T>)middleResult;
                    return querySet;
                }
            }
        }

    }
}
