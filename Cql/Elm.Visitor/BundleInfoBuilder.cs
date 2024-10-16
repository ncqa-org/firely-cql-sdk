using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hl7.Fhir.Model;

namespace Hl7.Cql.Elm.Visitor
{


    public static class BundleInfoBuilder
    {
        /// <summary>
        /// Returns a mapping between resources and valuesets
        /// </summary>
        /// <returns></returns>
        public static Dictionary<string, string[]> GetValueSetInfo(IEnumerable<DataRequirement> requirements)
        {
            Dictionary<string, string[]> result = new();
            foreach (var n in requirements)
            {
                
            }
            return result;
        }

        /// <summary>
        /// Returns a list of resources referenced in the provided statement.
        /// </summary>
        /// <returns></returns>
        public static Dictionary<string, string[]> GetMustSupportResourceInfo(IEnumerable<DataRequirement> requirements)
        {
            Dictionary<string, string[]> result = new();
            foreach (var n in requirements)
            {

            }
            return result;
        }
    }
}
