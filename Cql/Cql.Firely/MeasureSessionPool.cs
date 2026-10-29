/* 
 * Copyright (c) 2023, NCQA and contributors
 * See the file CONTRIBUTORS for details.
 * 
 * This file is licensed under the BSD 3-Clause license
 * available at https://raw.githubusercontent.com/FirelyTeam/firely-cql-sdk/main/LICENSE
 */

using System;
using System.Collections.Concurrent;
using Hl7.Cql.ValueSets;
using Hl7.Fhir.Model;

namespace Hl7.Cql.Fhir
{
    /// <summary>
    /// A thread-safe pool of <see cref="MeasureSession"/>s, for work that is not tied to a thread
    /// (async code, <c>Task</c>s, work queues). Each session is used by one caller at a time.
    /// </summary>
    /// <remarks>
    /// The pool never holds more sessions than the highest number of concurrent users it has seen,
    /// so it does not need a size limit: bound the concurrency instead, e.g. with <c>MaxDegreeOfParallelism</c>.
    /// </remarks>
    public sealed class MeasureSessionPool
    {
        private readonly ConcurrentBag<MeasureSession> _sessions = new();
        private readonly Func<MeasureSession> _factory;

        /// <param name="factory">Creates a new session, e.g. <c>() => new MeasureSession(FhirCqlContext.ForBundle(null, parameters, valueSets))</c>.</param>
        public MeasureSessionPool(Func<MeasureSession> factory)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        /// <summary>
        /// Takes a session from the pool, creating one if none is available. Give it back with <see cref="Return"/>.
        /// </summary>
        public MeasureSession Rent() => _sessions.TryTake(out var session) ? session : _factory();

        /// <summary>
        /// Gives a session back to the pool.
        /// </summary>
        public void Return(MeasureSession session)
        {
            ArgumentNullException.ThrowIfNull(session);
            _sessions.Add(session);
        }

        /// <summary>
        /// Rents a session, runs <see cref="MeasureSession.Evaluate{TResult}"/> on it, and returns it to the pool.
        /// </summary>
        public TResult Evaluate<TResult>(Bundle bundle, Func<MeasureSession, TResult> evaluate, IValueSetDictionary? valueSets = null)
        {
            var session = Rent();
            try
            {
                return session.Evaluate(bundle, evaluate, valueSets);
            }
            finally
            {
                Return(session);
            }
        }
    }
}
