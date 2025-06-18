/* 
 * Copyright (c) 2023, NCQA and contributors
 * See the file CONTRIBUTORS for details.
 * 
 * This file is licensed under the BSD 3-Clause license
 * available at https://raw.githubusercontent.com/FirelyTeam/firely-cql-sdk/main/LICENSE
 */

using Hl7.Cql.Iso8601;

namespace Hl7.Cql.Abstractions
{
    /// <summary>
    /// Defines UCUM unit constants.
    /// </summary>
    /// <see href="https://ucum.org/"/>
    public static class UCUMUnits
    {
        /// <summary>
        /// <see cref="Unary" />
        /// </summary>
        public const string Default = Unary;

        /// <summary>
        /// The unit code to represent unary, or "no units".
        /// Use this unit when expressing integers or decimals as quantities.
        /// </summary>
        public const string Unary = "1";

        /// <summary>
        /// Imperial inches
        /// </summary>
        public const string Inch = "[in_i]";
        /// <summary>
        /// Imperial feet
        /// </summary>
        public const string Foot = "[ft_i]";
        /// <summary>
        /// Imperial yards
        /// </summary>
        public const string Yard = "[yd_i]";
        /// <summary>
        /// Meters
        /// </summary>
        public const string Meter = "m";
        /// <summary>
        /// Centimeters
        /// </summary>
        public const string Centimeter = "cm";
    }


}
