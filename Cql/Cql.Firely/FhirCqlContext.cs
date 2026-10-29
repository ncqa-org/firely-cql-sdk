/* 
 * Copyright (c) 2023, NCQA and contributors
 * See the file CONTRIBUTORS for details.
 * 
 * This file is licensed under the BSD 3-Clause license
 * available at https://raw.githubusercontent.com/FirelyTeam/firely-cql-sdk/main/LICENSE
 */

using System;
using Hl7.Cql.Operators;
using Hl7.Cql.Runtime;
using Hl7.Cql.ValueSets;
using Hl7.Fhir.Model;

namespace Hl7.Cql.Fhir
{
    /// <summary>
    /// Factory methods to initialize an <see cref="CqlContext"/> that uses the SDK POCO model
    /// as binding for the Cql engine, supplying data using POCO instances.
    /// </summary>
    public static class FhirCqlContext
    {
        internal static CqlContext createContext(IDataSource? dataSource = null,
            IDictionary<string, object>? parameters = null,
            IValueSetDictionary? valueSets = null,
            DateTimeOffset? now = null,
            DefinitionDictionary<Delegate>? delegates = null,
            FhirModelBindingOptions? options = null) =>
            new CqlContext(
                new FhirModelBindingSetup(dataSource, valueSets, now, options).Operators,
                parameters,
                delegates);

        /// <summary>
        /// Factory method for creating a setup of the engine with the given <see cref="Bundle"/>.
        /// </summary>
        public static CqlContext ForBundle(Bundle? bundle = null,
            IDictionary<string, object>? parameters = null,
            IValueSetDictionary? valueSets = null,
            DateTimeOffset? now = null,
            DefinitionDictionary<Delegate>? delegates = null,
            FhirModelBindingOptions? options = null)
        {
            IDataSource source = bundle is not null ?
              new BundleDataSource(bundle, valueSets ?? new HashValueSetDictionary()) :
              new CompositeDataSource();

            return WithDataSource(source, parameters, valueSets, now, delegates, options);
        }

        /// <summary>
        /// Factory method for creating a setup of the engine with the given <see cref="IDataSource"/>.
        /// </summary>
        public static CqlContext WithDataSource(IDataSource? source = null,
            IDictionary<string, object>? parameters = null,
            IValueSetDictionary? valueSets = null,
            DateTimeOffset? now = null,
            DefinitionDictionary<Delegate>? delegates = null,
            FhirModelBindingOptions? options = null)
        {
            return createContext(source, parameters, valueSets, now, delegates, options);
        }

        /// <summary>
        /// Points an existing context at another patient's <paramref name="bundle"/>, keeping everything else: operators,
        /// comparers, value sets, parameters and message handlers.
        /// </summary>
        /// <remarks>
        /// Setting up a context (operators, comparers, unit conversion) costs far more than indexing a bundle, so a batch run
        /// can create one context per worker and switch the bundle for each patient instead of creating a context per patient.
        /// <para>
        /// A context is not thread-safe: use each one for one patient at a time. Library instances cache the results of their
        /// definitions, so call <see cref="CqlLibraryScope.Reset"/> on their scope between patients, or let a
        /// <see cref="MeasureSession"/> do it.
        /// </para>
        /// </remarks>
        /// <param name="context">A context created by <see cref="ForBundle"/> or <see cref="WithDataSource"/>.</param>
        /// <param name="bundle">The next patient's data.</param>
        /// <param name="valueSets">The value sets used for retrieves, or <see langword="null"/> to keep the context's value sets.</param>
        /// <returns><paramref name="context"/>.</returns>
        /// <exception cref="ArgumentException">The context was not created by <see cref="FhirCqlContext"/>.</exception>
        public static CqlContext SetBundle(this CqlContext context, Bundle bundle, IValueSetDictionary? valueSets = null)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(bundle);

            if (context.Operators is not CqlOperators operators)
                throw new ArgumentException($"The context must be created by {nameof(FhirCqlContext)}.", nameof(context));

            operators.DataSource = new BundleDataSource(bundle, valueSets ?? operators.ValueSets);
            return context;
        }
    }
}
