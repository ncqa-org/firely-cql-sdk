/* 
 * Copyright (c) 2023, NCQA and contributors
 * See the file CONTRIBUTORS for details.
 * 
 * This file is licensed under the BSD 3-Clause license
 * available at https://raw.githubusercontent.com/FirelyTeam/firely-cql-sdk/main/LICENSE
 */

using System;
using System.Collections.Generic;

namespace Hl7.Cql.Abstractions
{
    /// <summary>
    /// Utilities for converting between CQL and UCUM units.
    /// </summary>
    public static class Units
    {
        /// <summary>
        /// Maps CQL unit keywords (singular or plural) to their corresponding UCUM unit codes.
        /// </summary>
        /// <see href="https://www.hl7.org/fhir/valueset-ucum-units.html"/>
        public static readonly IDictionary<string, string> DatePrecisionToCqlUnits = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Year",           "year" },
            { "Month",          "month" },
            { "Day",            "day" },
            { "Week",           "week" },
            { "Hour",           "hour" },
            { "Minute",         "minute" },
            { "Second",         "second" },
            { "Millisecond",    "millisecond" }
        };
        

        /// <summary>
        /// Maps CQL unit keywords (singular or plural) to their corresponding UCUM unit codes.
        /// </summary>
        /// <see href="https://www.hl7.org/fhir/valueset-ucum-units.html"/>
        public static readonly List<string> CqlDateTimeUnits = new List<string>
        {
            "year", "years", "month", "months", "days", "day", "week", "weeks", "hour", "hours", "minute", "minutes", "second", "seconds", "millisecond", "milliseconds"
        };

    }
}
