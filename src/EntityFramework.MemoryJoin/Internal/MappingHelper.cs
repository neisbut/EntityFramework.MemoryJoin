using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using System.Text;
using System.Collections;
using System.Data.Common;
using System.Collections.Concurrent;
using System.Globalization;
#if NETSTANDARD1_5 || NETSTANDARD2_0
using Microsoft.EntityFrameworkCore;
#else
using System.Data.Entity;
#endif

namespace EntityFramework.MemoryJoin.Internal
{
    internal static class MappingHelper
    {

        static ConcurrentDictionary<Type, KnownProvider> typeToKnownProvider = new ConcurrentDictionary<Type, KnownProvider>();

        static void ValidateAndExtendMapping(Dictionary<Type, PropertyInfo[]> mapping)
        {
            if (!mapping.ContainsKey(typeof(long)))
                throw new InvalidOperationException("Please include at least one property with `Long` type");

            if (!mapping.ContainsKey(typeof(double)))
                throw new InvalidOperationException("Please include at least one property with `Double` type");

            if (!mapping.ContainsKey(typeof(int)))
                mapping[typeof(int)] = mapping[typeof(long)];

            if (!mapping.ContainsKey(typeof(Single)))
                mapping[typeof(Single)] = mapping[typeof(Double)];

            if (!mapping.ContainsKey(typeof(decimal)))
                mapping[typeof(decimal)] = mapping[typeof(Double)];
        }

        internal static Dictionary<Type, PropertyInfo[]> GetPropertyMappings(Type queryClass)
        {
            var allowedMapping = queryClass.
                GetProperties().
                Where(x => x.GetCustomAttribute<KeyAttribute>() == null).
                GroupBy(x => Nullable.GetUnderlyingType(x.PropertyType) ?? x.PropertyType).
                ToDictionary(x => x.Key, x => x.ToArray());
            ValidateAndExtendMapping(allowedMapping);

            return allowedMapping;
        }

        internal static Mapping<T> GetEntityMapping<T>(
            DbContext context,
            Type queryClass,
            Dictionary<Type, PropertyInfo[]> allowedPropertyMapping
            )
        {
            HashSet<PropertyInfo> allowedProperties = new HashSet<PropertyInfo>(
                allowedPropertyMapping.SelectMany(x => x.Value));

            var inParam = Expression.Parameter(typeof(T), "x");
            var inMappingPairs = new List<Tuple<MemberInfo, Expression>>();

            var outParam = Expression.Parameter(queryClass, "x");
            var outMappingPairs = new List<Tuple<MemberInfo, Expression>>();

            var usedProperties = new Dictionary<string, Func<T, object>>();
            var columnNamesDict = EFHelper.GetColumnNames(context, queryClass);

            foreach (var prop in typeof(T).GetProperties())
            {
                var baseType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                if (!allowedPropertyMapping.TryGetValue(baseType, out PropertyInfo[] allowedMappedProps))
                    throw new NotSupportedException("Not supported property type");

                var mapProperty = allowedMappedProps.FirstOrDefault(x => allowedProperties.Contains(x));
                if (mapProperty == null)
                    throw new NotSupportedException("Too complex object");

                Expression inExp = Expression.MakeMemberAccess(inParam, prop);
                if (mapProperty.PropertyType != prop.PropertyType)
                    inExp = Expression.Convert(inExp, mapProperty.PropertyType);

                inMappingPairs.Add(new Tuple<MemberInfo, Expression>(mapProperty, inExp));

                Expression outExp = Expression.MakeMemberAccess(outParam, mapProperty);
                if (mapProperty.PropertyType != prop.PropertyType)
                    outExp = Expression.Convert(outExp, prop.PropertyType);

                outMappingPairs.Add(new Tuple<MemberInfo, Expression>(prop, outExp));

                allowedProperties.Remove(mapProperty);

                usedProperties.Add(
                    columnNamesDict[mapProperty],
                    (Func<T, object>)(Expression.Lambda(
                        Expression.Convert(inExp, typeof(object)),
                        inParam
                    ).Compile()));
            }

            var inCtor = queryClass.GetConstructor(new Type[] { });
            var inNew = Expression.New(inCtor);
            var inBind = Expression.MemberInit(inNew,
                inMappingPairs.Select(x => Expression.Bind(x.Item1, x.Item2)));
            var inExpression = Expression.Lambda(inBind, inParam);

            var outCtor = typeof(T).GetConstructors(
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            var emptyCtor = outCtor.FirstOrDefault(x => x.GetParameters().Length == 0);
            Expression outExpression;

            if (emptyCtor != null)
            {
                var outNew = Expression.New(emptyCtor);
                var outBind = Expression.MemberInit(outNew,
                    outMappingPairs.Select(x => Expression.Bind(x.Item1, x.Item2)));
                outExpression = Expression.Lambda(outBind, outParam);
            }
            else
            {
                var outNew = Expression.New(
                    outCtor.First(),
                    outMappingPairs.Select(x => x.Item2).ToArray(),
                    outMappingPairs.Select(x => x.Item1).ToArray());
                outExpression = Expression.Lambda(outNew, outParam);
            }

            return new Mapping<T>()
            {
                UserProperties = usedProperties,
                OutExpression = outExpression
            };
        }

        internal static void ComposeTableSql(
            StringBuilder stringBuilder,
            InterceptionOptions options,
            DbCommand command,
            IList parameters)
        {
            var providerType = typeToKnownProvider.GetOrAdd(
                options.ContextType, (t) => GetKnownProvider(command));

            var paramPattern = "@__gen_q_p";

            var sb = stringBuilder;
            var innerSb = new StringBuilder(20);
            sb.Append("SELECT * FROM (VALUES ");

            var i = 0;
            foreach (var el in options.Data)
            {
                sb.Append("(");
                for (var j = 0; j < options.ColumnNames.Length; j++)
                {
                    var value = el[options.ColumnNames[j]];
                    string stringValue = TryProcessParameterAsString(value, command, providerType, innerSb, options.ValuesInjectMethod);

                    if (stringValue != null)
                    {
                        sb.Append(stringValue);
                    }
                    else
                    {
                        var paramName = $"{paramPattern}{i}";
                        var param = command.CreateParameter();
                        param.ParameterName = paramName;
                        param.Value = value;
                        parameters.Add(param);
                        sb.Append(paramName);

                        i++;
                    }
                    sb.Append(", ");
                }
                sb.Length -= 2;
                sb.Append("), ");
            }

            sb.Length -= 2;

            sb.Append(") AS ").Append(options.DynamicTableName).Append(" (");
            foreach (var cname in options.ColumnNames)
            {
                sb.Append(cname).Append(", ");
            }
            sb.Length -= 2;
            sb.Append(")");
        }

        static string TryProcessParameterAsString(
            object value,
            DbCommand command,
            KnownProvider provider,
            StringBuilder sb,
            ValuesInjectionMethodInternal injectMethod)
        {
            // null is just 'NULL'
            if (value == null)
                return "NULL";

            if (injectMethod == ValuesInjectionMethodInternal.ViaParameters)
            {
                return null;
            }
            else if (injectMethod == ValuesInjectionMethodInternal.Auto)
            {
                // Postgres has a huge limit for parameters, whereas MSSQL is just ... 2100 :(
                if (provider == KnownProvider.PostgreSQL)
                    return null;
            }

            // Try to inject parameters as text
            sb.Length = 0;
            if (value is string strValue)
            {
                sb.Append("'").Append(strValue.Replace("'", "''")).Append("'");
            }
            else if (value is int || value is long ||
                value is float || value is double ||
                value is decimal)
            {
                sb.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
            }
            else if (value is DateTime dateValue)
            {
                if (provider == KnownProvider.Mssql)
                {
                    sb.Append("CAST ('")
                        .Append(dateValue.ToString("yyyy-MM-ddTHH:mm:ss.fff"))
                        .Append("' AS DATETIME)");
                }
                else if(provider == KnownProvider.PostgreSQL)
                {
                    sb.Append("'")
                        .Append(dateValue.ToString("yyyy-MM-ddTHH:mm:ss.fff"))
                        .Append("'::date");
                }
                else
                {
                    // other providers are not yet implemented
                    return null;
                }
            }
            else
            {
                // can't process as string
                return null;
            }

            return sb.ToString();
        }

        static KnownProvider GetKnownProvider(DbCommand command)
        {
            if (command.GetType().Name.StartsWith("Npgsql"))
                return KnownProvider.PostgreSQL;
            if (command.GetType().Name.StartsWith("SqlCommand"))
                return KnownProvider.Mssql;

            return KnownProvider.Unknown;
        }

    }
}
