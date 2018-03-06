using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace EntityFramework.MemoryJoin.Internal
{
    internal class Mapping<T>
    {
        public Dictionary<string, Func<T, object>> UserProperties { get; internal set; }

        public Expression OutExpression { get; internal set; }
    }
}
