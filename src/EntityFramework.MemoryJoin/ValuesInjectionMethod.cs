using EntityFramework.MemoryJoin.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EntityFramework.MemoryJoin
{
    /// <summary>
    /// States a method of parameters injection into SQL query
    /// </summary>
    public enum ValuesInjectionMethod
    {
        /// <summary>
        /// Method will be picked automatically
        /// </summary>
        Auto = ValuesInjectionMethodInternal.Auto,

        /// <summary>
        /// ALL values will go as parameters
        /// Pros: values will go safely
        /// Cons: there is a limit on parametes count in MS SQL, max is 2100 parameters
        /// </summary>
        ViaParameters = ValuesInjectionMethodInternal.ViaParameters,

        /// <summary>
        /// Values will be injected as a text to SQL query
        /// </summary>
        ViaSqlQueryBody = ValuesInjectionMethodInternal.ViaSqlQueryBody
    }
}
