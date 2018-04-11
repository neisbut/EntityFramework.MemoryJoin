using System;
using EntityFramework.MemoryJoin.Internal;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.Entity;
using System.Data.Entity.Infrastructure.Interception;
using System.Text;

namespace EntityFramework.MemoryJoin
{
    internal class MemoryJoinerInterceptor : IDbCommandInterceptor
    {
        private static readonly ConcurrentDictionary<DbContext, InterceptionOptions> InterceptionOptions =
            new ConcurrentDictionary<DbContext, InterceptionOptions>();

        internal static void SetInterception(DbContext context, InterceptionOptions options)
        {
            InterceptionOptions[context] = options;
        }

        internal static bool IsInterceptionEnabled(IEnumerable<DbContext> contexts, out InterceptionOptions options)
        {
            options = null;
            using (var enumerator = contexts.GetEnumerator())
            {
                if (!enumerator.MoveNext()) return false;

                var firstOne = enumerator.Current;
                var result = firstOne != null &&
                             InterceptionOptions.TryGetValue(firstOne, out options) &&
                             !enumerator.MoveNext();
                if (result)
                    InterceptionOptions.TryRemove(firstOne, out options);

                return result;
            }
        }

        public void NonQueryExecuted(DbCommand command, DbCommandInterceptionContext<int> interceptionContext)
        {
        }

        public void NonQueryExecuting(DbCommand command, DbCommandInterceptionContext<int> interceptionContext)
        {
            if (IsInterceptionEnabled(interceptionContext.DbContexts, out var opts))
                ModifyQuery(command, opts);
        }

        public void ReaderExecuted(DbCommand command, DbCommandInterceptionContext<DbDataReader> interceptionContext)
        {
        }

        public void ReaderExecuting(DbCommand command, DbCommandInterceptionContext<DbDataReader> interceptionContext)
        {
            if (IsInterceptionEnabled(interceptionContext.DbContexts, out var opts))
                ModifyQuery(command, opts);
        }

        public void ScalarExecuted(DbCommand command, DbCommandInterceptionContext<object> interceptionContext)
        {
        }

        public void ScalarExecuting(DbCommand command, DbCommandInterceptionContext<object> interceptionContext)
        {
            if (IsInterceptionEnabled(interceptionContext.DbContexts, out var opts))
                ModifyQuery(command, opts);
        }

        private static void ModifyQuery(DbCommand command, InterceptionOptions opts)
        {
            var tableNamePosition = command.CommandText.IndexOf(opts.QueryTableName, StringComparison.Ordinal);
            if (tableNamePosition < 0)
                return;

            var nextSpace = command.CommandText.IndexOf(' ', tableNamePosition);
            var prevSpace = command.CommandText.LastIndexOf(' ', tableNamePosition);
            var tableFullName = command.CommandText.Substring(prevSpace + 1, nextSpace - prevSpace - 1);

            command.CommandText = command.CommandText.Replace(tableFullName, opts.DynamicTableName);

            var sb = new StringBuilder(100);
            sb.Append("WITH ").Append(opts.DynamicTableName).Append(" AS (").AppendLine();
            MappingHelper.ComposeTableSql(
                sb, opts,
                command,
                command.Parameters);

            sb.AppendLine();
            sb.AppendLine(")");
            sb.Append(command.CommandText);

            command.CommandText = sb.ToString();
        }
    }
}
