#define PROFILE_VALUE_REUSE
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Hl7.Cql.Abstractions
{
    /// <summary>
    /// 
    /// </summary>
    public class CachedBool
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        public CachedBool(Func<bool?> value) { this.getValue = value; }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="cachedBool"></param>
        public static implicit operator bool?(CachedBool cachedBool)
        {
            bool? result = cachedBool.GetValue();
            return result;
        }

        /// <summary>
        /// 
        /// </summary>
        public static long ReusedValueCount;

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public bool? GetValue()
        {
            bool? result = null;
            if(ranValue)
            {
                result = _value;
#if PROFILE_VALUE_REUSE
                Interlocked.Increment(ref ReusedValueCount);
#endif
            }
            else
            {
                _value = getValue();
                ranValue = true;
                result = _value;
            }
            return result;
        }

        private bool ranValue;
        private readonly Func<bool?> getValue;
        private bool? _value;
    }
}
