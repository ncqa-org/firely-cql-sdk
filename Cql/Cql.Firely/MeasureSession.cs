/* 
 * Copyright (c) 2023, NCQA and contributors
 * See the file CONTRIBUTORS for details.
 * 
 * This file is licensed under the BSD 3-Clause license
 * available at https://raw.githubusercontent.com/FirelyTeam/firely-cql-sdk/main/LICENSE
 */

using System;
using System.Collections.Generic;
using Hl7.Cql.Runtime;
using Hl7.Cql.ValueSets;
using Hl7.Fhir.Model;

namespace Hl7.Cql.Fhir
{
    /// <summary>
    /// Evaluates any number of generated measures for many patients on one thread, without allocating a context
    /// or measure instances per patient.
    /// </summary>
    /// <remarks>
    /// A session owns one <see cref="CqlContext"/> and one <see cref="CqlLibraryScope"/>. Measures are created the first time
    /// they are requested with <see cref="Get{T}"/> and then reused; libraries that several measures include (FHIRHelpers, ...)
    /// exist once, so their definitions are evaluated once per patient however many measures use them.
    /// Values that depend on the patient are discarded after each <see cref="Evaluate{TResult}"/>; values that only depend on
    /// parameters and value sets are kept.
    /// <para>
    /// A session is not thread-safe. Use one per worker thread, or rent them from a <see cref="MeasureSessionPool"/>.
    /// Results returned from <see cref="Evaluate{TResult}"/> must not be lazy sequences over the patient's data:
    /// materialize or consume them inside the callback.
    /// </para>
    /// </remarks>
    public sealed class MeasureSession
    {
        /// <summary>
        /// Creates a session for a context made by <see cref="FhirCqlContext"/>.
        /// </summary>
        public MeasureSession(CqlContext context)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// The context shared by all measures in this session.
        /// </summary>
        public CqlContext Context { get; }

        /// <summary>
        /// The scope holding all library instances of this session.
        /// </summary>
        public CqlLibraryScope Scope { get; } = new();

        /// <summary>
        /// Gets the instance of the generated measure or library <typeparamref name="T"/>, creating it on first use.
        /// </summary>
        public T Get<T>() where T : class, ICqlLibraryInstance => (T)Get(typeof(T));

        /// <summary>
        /// Gets the instance of the generated measure or library of type <paramref name="type"/>, creating it on first use.
        /// Use this for types loaded from an assembly at run time.
        /// </summary>
        public ICqlLibraryInstance Get(Type type)
        {
            ArgumentNullException.ThrowIfNull(type);
            return Scope.GetOrAdd(type, scope =>
                Activator.CreateInstance(type, Context, scope) as ICqlLibraryInstance
                ?? throw new InvalidOperationException($"{type.FullName} is not a generated CQL library with a constructor taking a {nameof(CqlContext)} and a {nameof(CqlLibraryScope)}."));
        }

        /// <summary>
        /// Points the session at a patient's <paramref name="bundle"/>, runs <paramref name="evaluate"/>, and then discards
        /// everything cached for that patient.
        /// </summary>
        /// <param name="bundle">The patient's data.</param>
        /// <param name="evaluate">Reads the results from the measures, e.g. using <see cref="Get{T}"/>.</param>
        /// <param name="valueSets">The value sets used for retrieves, or <see langword="null"/> to keep the current ones.</param>
        public TResult Evaluate<TResult>(Bundle bundle, Func<MeasureSession, TResult> evaluate, IValueSetDictionary? valueSets = null)
        {
            ArgumentNullException.ThrowIfNull(bundle);
            ArgumentNullException.ThrowIfNull(evaluate);

            Context.SetBundle(bundle, valueSets);
            try
            {
                return evaluate(this);
            }
            finally
            {
                Scope.Reset();
            }
        }

        /// <summary>
        /// Replaces the parameters of the context and discards every cached value, including those that only depend on parameters.
        /// </summary>
        public void SetParameters(IDictionary<string, object> parameters)
        {
            ArgumentNullException.ThrowIfNull(parameters);
            Context.Parameters = parameters;
            Scope.ResetAll();
        }
    }
}
