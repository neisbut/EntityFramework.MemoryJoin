using System;
using System.Collections.Generic;

namespace EntityFramework.MemoryJoin.Internal
{
    internal class InterceptionOptions
    {
        public string DynamicTableName = "__gen_query_data__";

        public string QueryTableName { get; set; }

        public List<Dictionary<string, object>> Data { get; set; }

        public string[] ColumnNames { get; set; }

        public Type ContextType { get; set; }

    }
}
