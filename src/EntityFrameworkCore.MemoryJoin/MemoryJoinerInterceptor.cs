using System;
using EntityFramework.MemoryJoin.Internal;
using System.Collections.Concurrent;
using System.Data.Common;
using System.Text;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Internal;



#if !NETSTANDARD
using TaskTypeInt = System.Threading.Tasks.ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int>>;
using TaskTypeReader = System.Threading.Tasks.ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<System.Data.Common.DbDataReader>>;
using TaskTypeObject = System.Threading.Tasks.ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<System.Object>>;
#else
using TaskTypeInt = System.Threading.Tasks.Task<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int>>;
using TaskTypeReader = System.Threading.Tasks.Task<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<System.Data.Common.DbDataReader>>;
using TaskTypeObject = System.Threading.Tasks.Task<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<System.Object>>;
#endif

namespace EntityFrameworkCore.MemoryJoin
{
    public class MemoryJoinerInterceptor : DbCommandInterceptor
    {
        private static readonly ConcurrentDictionary<DbContext, InterceptionOptions> InterceptionOptions =
            new ConcurrentDictionary<DbContext, InterceptionOptions>();

        internal static void SetInterception(DbContext context, InterceptionOptions options)
        {
            InterceptionOptions[context] = options;
        }

        internal static bool IsInterceptionEnabled(DbContext context, DbCommand cmd, out InterceptionOptions options)
        {
            if (MemoryJoiner.MemoryJoinerMode != MemoryJoinerMode.UsingInterception)
            {
                options = null;
                return false;
            }

            var result = InterceptionOptions.TryGetValue(context, out options);
            if (result)
            {
                if (cmd == null)
                    InterceptionOptions.TryRemove(context, out options);
                else
                {
                    if (Regex.IsMatch(cmd.CommandText, $"\\s+(\\\"?\\S+\\\"?)?\\\"?{options.QueryTableName}\\\"?\\s+"))
                        InterceptionOptions.TryRemove(context, out options);
                    else
                        return false;
                }
            }

            return result;
        }

        public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
        {
            if (IsInterceptionEnabled(eventData.Context, command, out var opts))
                ModifyQuery(command, opts);
            return base.NonQueryExecuting(command, eventData, result);
        }

        public override TaskTypeInt NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (IsInterceptionEnabled(eventData.Context, command, out var opts))
                ModifyQuery(command, opts);
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            if (IsInterceptionEnabled(eventData.Context, command, out var opts))
                ModifyQuery(command, opts);
            return base.ReaderExecuting(command, eventData, result);
        }

        public override TaskTypeReader ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (IsInterceptionEnabled(eventData.Context, command, out var opts))
                ModifyQuery(command, opts);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override InterceptionResult<object> ScalarExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
        {
            if (IsInterceptionEnabled(eventData.Context, command, out var opts))
                ModifyQuery(command, opts);
            return base.ScalarExecuting(command, eventData, result);
        }

        public override TaskTypeObject ScalarExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
        {
            if (IsInterceptionEnabled(eventData.Context, command, out var opts))
                ModifyQuery(command, opts);
            return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
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
