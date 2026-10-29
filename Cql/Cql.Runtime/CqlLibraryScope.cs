/* 
 * Copyright (c) 2023, NCQA and contributors
 * See the file CONTRIBUTORS for details.
 * 
 * This file is licensed under the BSD 3-Clause license
 * available at https://raw.githubusercontent.com/FirelyTeam/firely-cql-sdk/main/LICENSE
 */

using System;
using System.Collections.Generic;

namespace Hl7.Cql.Runtime
{
    /// <summary>
    /// Implemented by generated library classes that cache the values of their definitions.
    /// </summary>
    public interface ICqlLibraryInstance
    {
        /// <summary>
        /// The scope that owns this instance and all libraries it depends on.
        /// </summary>
        CqlLibraryScope Scope { get; }

        /// <summary>
        /// Discards the cached values of this instance that depend on the patient's data.
        /// Does not reset dependencies; use <see cref="CqlLibraryScope.Reset"/> for that.
        /// </summary>
        void ResetPatientValues();

        /// <summary>
        /// Discards the cached values of this instance that do not depend on the patient's data
        /// (value sets, parameters and anything derived only from them).
        /// Does not reset dependencies; use <see cref="CqlLibraryScope.ResetAll"/> for that.
        /// </summary>
        void ResetSharedValues();
    }

    /// <summary>
    /// Owns the library instances of one or more measures that are evaluated with the same <see cref="CqlContext"/>,
    /// so that a library included by several libraries or measures is instantiated, and evaluated, only once.
    /// </summary>
    /// <remarks>
    /// A scope and the instances in it are not thread-safe: use them from one thread at a time,
    /// typically one scope per worker.
    /// </remarks>
    public sealed class CqlLibraryScope
    {
        private readonly Dictionary<Type, ICqlLibraryInstance> _instances = new();
        private readonly List<ICqlLibraryInstance> _ordered = new();

        /// <summary>
        /// Adds a library instance to this scope. Generated constructors call this.
        /// </summary>
        /// <exception cref="InvalidOperationException">An instance of the same type is already in this scope.</exception>
        public void Register(ICqlLibraryInstance instance)
        {
            ArgumentNullException.ThrowIfNull(instance);
            if (!_instances.TryAdd(instance.GetType(), instance))
                throw new InvalidOperationException($"An instance of {instance.GetType().FullName} already exists in this scope. Use {nameof(GetOrAdd)} to share it.");
            _ordered.Add(instance);
        }

        /// <summary>
        /// Gets the instance of <typeparamref name="T"/> in this scope, creating it with <paramref name="factory"/> if there is none yet.
        /// </summary>
        public T GetOrAdd<T>(Func<CqlLibraryScope, T> factory) where T : class, ICqlLibraryInstance
        {
            ArgumentNullException.ThrowIfNull(factory);
            if (_instances.TryGetValue(typeof(T), out var existing))
                return (T)existing;

            // The constructor registers itself in this scope.
            var created = factory(this);
            return _instances.TryGetValue(typeof(T), out existing) && ReferenceEquals(existing, created)
                ? created
                : throw new InvalidOperationException($"{typeof(T).FullName} did not register itself in the scope it was created with.");
        }

        /// <summary>
        /// Gets the instance of type <paramref name="type"/> in this scope, creating it with <paramref name="factory"/> if there is none yet.
        /// </summary>
        public ICqlLibraryInstance GetOrAdd(Type type, Func<CqlLibraryScope, ICqlLibraryInstance> factory)
        {
            ArgumentNullException.ThrowIfNull(type);
            ArgumentNullException.ThrowIfNull(factory);
            if (_instances.TryGetValue(type, out var existing))
                return existing;

            var created = factory(this);
            return _instances.TryGetValue(type, out existing) && ReferenceEquals(existing, created)
                ? created
                : throw new InvalidOperationException($"{type.FullName} did not register itself in the scope it was created with.");
        }

        /// <summary>
        /// Discards the cached values that depend on the patient's data in every instance in this scope.
        /// Call this between patients.
        /// </summary>
        public void Reset()
        {
            foreach (var instance in _ordered)
                instance.ResetPatientValues();
        }

        /// <summary>
        /// Discards all cached values in every instance in this scope, including those that only depend on
        /// parameters. Call this when the parameters or the evaluation time of the context change.
        /// </summary>
        public void ResetAll()
        {
            foreach (var instance in _ordered)
            {
                instance.ResetPatientValues();
                instance.ResetSharedValues();
            }
        }
    }
}
