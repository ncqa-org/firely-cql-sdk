using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hl7.Cql.Runtime
{
    /// <summary>
    /// Provides extension methods for the <see cref="CqlContext"/> class.
    /// </summary>
    public static class CqlContextExtensions
    {
        /// <summary>
        /// Creates a shallow copy of the given <see cref="CqlContext"/> instance.
        /// Note: Only the <see cref="CqlContext.Parameters"/> dictionary is cloned; other reference-type fields are reused.
        /// For a deep copy, clone all reference-type fields as needed.
        /// </summary>
        /// <param name="original">The original <see cref="CqlContext"/> to clone.</param>
        /// <returns>A new <see cref="CqlContext"/> instance with copied parameters.</returns>
        public static CqlContext Clone(this CqlContext original)
        {
            return new CqlContext(original.Operators, original.Parameters, original.Definitions);
        }
    }
}
