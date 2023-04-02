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
        private static readonly ConcurrentDictionary<DbContext, List<InterceptionOptions>> InterceptionOptions =
            new ConcurrentDictionary<DbContext, List<InterceptionOptions>>();

        private static readonly Object Locker = new object();

        internal static void SetInterception(DbContext context, InterceptionOptions options)
        {
            lock (Locker)
            {
                if (!InterceptionOptions.TryGetValue(context, out var opts))
                {
                    opts = new List<InterceptionOptions>();
                    InterceptionOptions[context] = opts;
                }

                opts.Add(options);
            }
        }

        internal static bool IsInterceptionEnabled(IEnumerable<DbContext> contexts, bool removeContextOptions,
            out IReadOnlyList<InterceptionOptions> options)
        {
            lock (Locker)
            {
                options = null;
                List<InterceptionOptions> internalOptions = null;
                using (var enumerator = contexts.GetEnumerator())
                {
                    if (!enumerator.MoveNext()) return false;

                    var firstOne = enumerator.Current;
                    var result = firstOne != null &&
                                 InterceptionOptions.TryGetValue(firstOne, out internalOptions) &&
                                 !enumerator.MoveNext();
                    options = internalOptions;

                    if (result && removeContextOptions)
                        InterceptionOptions.TryRemove(firstOne, out _);

                    return result;
                }
            }
        }

        public void NonQueryExecuted(DbCommand command, DbCommandInterceptionContext<int> interceptionContext)
        {
        }

        public void NonQueryExecuting(DbCommand command, DbCommandInterceptionContext<int> interceptionContext)
        {
            if (IsInterceptionEnabled(interceptionContext.DbContexts, true, out var opts))
                ModifyQuery(command, opts);
        }

        public void ReaderExecuted(DbCommand command, DbCommandInterceptionContext<DbDataReader> interceptionContext)
        {
        }

        public void ReaderExecuting(DbCommand command, DbCommandInterceptionContext<DbDataReader> interceptionContext)
        {
            if (IsInterceptionEnabled(interceptionContext.DbContexts, true, out var opts))
                ModifyQuery(command, opts);
        }

        public void ScalarExecuted(DbCommand command, DbCommandInterceptionContext<object> interceptionContext)
        {
        }

        public void ScalarExecuting(DbCommand command, DbCommandInterceptionContext<object> interceptionContext)
        {
            if (IsInterceptionEnabled(interceptionContext.DbContexts, true, out var opts))
                ModifyQuery(command, opts);
        }

        private static void ModifyQuery(DbCommand command, IReadOnlyList<InterceptionOptions> opts)
        {
            var sb = new StringBuilder(100);
            sb.Append("WITH ");
            var counter = 0;
            var commandStart = 0;
            foreach (var currentOptions in opts)
            {
                var tableNamePosition =
                    command.CommandText.IndexOf(currentOptions.QueryTableName, StringComparison.Ordinal);
                if (tableNamePosition < 0)
                    continue;

                commandStart = command.CommandText.LastIndexOf(';', tableNamePosition) + 1;

                var nextSpace = command.CommandText.IndexOf(' ', tableNamePosition);
                var prevSpace = command.CommandText.LastIndexOf(' ', tableNamePosition);
                var tableFullName = command.CommandText.Substring(prevSpace + 1, nextSpace - prevSpace - 1);

                command.CommandText = command.CommandText.Replace(tableFullName, currentOptions.DynamicTableName);

                if (counter > 0)
                {
                    sb.AppendLine(",");
                }

                sb.Append(currentOptions.DynamicTableName).Append(" AS (").AppendLine();

                MappingHelper.ComposeTableSql(
                    sb, currentOptions,
                    command,
                    command.Parameters);

                sb.AppendLine();
                sb.AppendLine(")");

                counter++;
            }

            command.CommandText = command.CommandText.Insert(commandStart, sb.ToString());
        }
    }
}