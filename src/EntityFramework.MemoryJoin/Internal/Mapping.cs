using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace EntityFramework.MemoryJoin.Internal
{
    internal class Mapping<T>
    {
        public Dictionary<string, Func<T, object>> UserProperties { get; internal set; }

        public Expression OutExpression { get; internal set; }

        public string KeyColumnName { get; internal set; }
    }
}
