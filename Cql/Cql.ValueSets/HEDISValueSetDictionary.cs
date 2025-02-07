/* 
 * Copyright (c) 2023, NCQA and contributors
 * See the file CONTRIBUTORS for details.
 * 
 * This file is licensed under the BSD 3-Clause license
 * available at https://raw.githubusercontent.com/FirelyTeam/firely-cql-sdk/main/LICENSE
 */

using Hl7.Cql.Primitives;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Hl7.Cql.ValueSets
{

    /// <summary>
    /// Uses hash sets to identify code membership within value sets.
    /// </summary>
    public class HEDISValueSetDictionary : IValueSetDictionary
    {
        private const string NullCodeSystem = "\0";
        private readonly CqlCodeHasher _codeHasher = new();

        private static int[] useOffsetArray = new int[86];
        static HEDISValueSetDictionary()
        {
            useOffsetArray[27] = 1;
            useOffsetArray[33] = 1;
        }

        /// <summary>
        /// Adds the code to the given value set by its canonical URI.
        /// </summary>
        /// <param name="valueSetUri">The value set's canonical URI.</param>
        /// <param name="code">The code to add.</param>
        /// <exception cref="ArgumentException">If <paramref name="code"/> already exists in the specified value set.</exception>
        public void Add(string valueSetUri, CqlCode code)
        {
            if (string.IsNullOrEmpty(valueSetUri))
            {
                throw new ArgumentException($"'{nameof(valueSetUri)}' cannot be null or empty.", nameof(valueSetUri));
            }

            if (code is null)
            {
                throw new ArgumentNullException(nameof(code));
            }

            _codesByHash.Add(GetKey(valueSetUri, code.code ?? "", code.system ?? NullCodeSystem), code);
            var nullKey = GetKey(valueSetUri, code.code ?? "", NullCodeSystem);
            _codesByHash.TryAdd(nullKey, code);

            if (!_codesInValueSet.TryGetValue(valueSetUri, out var codes))
            {
                codes = new HashSet<CqlCode>(_codeHasher) { code };
                _codesInValueSet.Add(valueSetUri, codes);
            }
            else
                codes.Add(code);
        }

        /// <summary>
        /// Adds or overwrites the code to the given value set by its canonical URI, and will not throw if the code exists already.
        /// </summary>
        /// <param name="valueSetUri">The value set's canonical URI.</param>
        /// <param name="code">The code to add.</param>
        public void Set(string valueSetUri, CqlCode code)
        {
            _codesByHash[GetKey(valueSetUri, code.code ?? "", code.system ?? "")] = code;
            _codesByHash[GetKey(valueSetUri, code.code ?? "", NullCodeSystem)] = code;
            if (!_codesInValueSet.TryGetValue(valueSetUri, out var codes))
            {
                codes = new HashSet<CqlCode>(_codeHasher)
                {
                    code
                };
                _codesInValueSet.Add(valueSetUri, codes);
            }
            else
                codes.Add(code);
        }

        /// <summary>
        /// Returns <see langword="true"/> if the given code is present in the given value set.
        /// This method ignores the code system of the codes present in <paramref name="valueSetUri"/>.
        /// This method approaches an O(1) operation.
        /// </summary>
        /// <param name="valueSetUri">The value set's canonical URI.</param>
        /// <param name="code">The code to check.</param>
        /// <returns><see langword="true"/> if the given code is present in the given value set.</returns>

        public bool IsCodeInValueSet(string valueSetUri, string code) =>
            _codesByHash.ContainsKey(GetKey(valueSetUri, code, NullCodeSystem));


        /// <summary>
        /// Returns <see langword="true"/> if the given code is present in the given value set.
        /// This method approaches an O(1) operation.
        /// </summary>
        /// <param name="valueSetUri">The value set's canonical URI.</param>
        /// <param name="code">The code to check.</param>
        /// <param name="systemUriOrOid">The code system's canonical URI or its OID.</param>
        /// <returns><see langword="true"/> if the given code is present in the given value set.</returns>
        public bool IsCodeInValueSet(string valueSetUri, string code, string systemUriOrOid) =>
            _codesByHash.ContainsKey(GetKey(valueSetUri, code, systemUriOrOid));

        /// <summary>
        /// Tries to ge the codes in the value set as an <see cref="IReadOnlyCollection{CqlCode}"/>.
        /// </summary>
        /// <param name="valueSetUri">The value set's canonical URI.</param>
        /// <param name="codes">The <see langword="out"/> parameter for the value set's codes, or <see langword="null"/>.</param>
        /// <returns><see langword="true"/> if the given value set is defined; otherwise, <see langword="false"/>.</returns>
        public bool TryGetCodesInValueSet(string valueSetUri, out IReadOnlyCollection<CqlCode>? codes)
        {
            if (_codesInValueSet.TryGetValue(valueSetUri, out var codeSet))
            {
                codes = codeSet;
                return true;
            }
            codes = null!;
            return false;
        }

        /// <summary>
        /// Gets the total number of codes in all value sets in this dictionary.
        /// </summary>
        public int Count => _codesByHash.Count / 2;

        private static int HashSystem(string systemString)
        {
            //NOTE(agw): each system is _almost_ a unique length.
            // of the ones that are not a unique length, you can offset by one based on the final
            // character. useOffsetArray determines if we need to do this.
            int useOffset = useOffsetArray[systemString.Length];
            int offset = (systemString[systemString.Length - 1] > 'm' ? 1 : 0);
            int systemHash = systemString.Length + offset * useOffset;
            return systemHash;
        }

        static readonly ulong PRIME64_1 = 0x9E3779B185EBCA87;
        static readonly ulong PRIME64_2 = 0xC2B2AE3D27D4EB4F;
        static readonly ulong PRIME64_3 = 0x85EBCA77C2B2AE63;

        private static ulong xxHash64_RotateLeft(ulong x, byte bits)
        {
            ulong result = (x << bits) | (x >> (64 - bits));
            return result;
        }
        private static ulong xxHash64_ProcessSingle(ulong previous, ulong input)
        {
            ulong result = xxHash64_RotateLeft(previous + input * PRIME64_2, 31) * PRIME64_1;
            return result;
        }

        private static ulong GetKey(string valueSetUri, string code, string systemUri)
        {
            // NOTE(agw): When writing this, the max code length in all HEDIS value sets was 20
            Span<byte> bytes = stackalloc byte[32];
            int systemHash = HashSystem(systemUri);

            // get valueset hash
            ulong valuesetHash = 0;
            {
                // get last 4 chars as unique hash
                ReadOnlySpan<char> last4 = valueSetUri.AsSpan();
                ref char last4Ref = ref MemoryMarshal.GetReference<char>(last4);
                ref char minus4 = ref Unsafe.Add(ref last4Ref, valueSetUri.Length - 4);
                ref char minus3 = ref Unsafe.Add(ref last4Ref, valueSetUri.Length - 3);
                ref char minus2 = ref Unsafe.Add(ref last4Ref, valueSetUri.Length - 2);
                ref char minus1 = ref Unsafe.Add(ref last4Ref, valueSetUri.Length - 1);

                valuesetHash = valuesetHash | ((ulong)minus4) << 48;
                valuesetHash = valuesetHash | ((ulong)minus3) << 32;
                valuesetHash = valuesetHash | ((ulong)minus2) << 16;
                valuesetHash = valuesetHash | ((ulong)minus1) << 0;
            }

            // copy / compress various hashes into one
            Span<int> systemDest = MemoryMarshal.Cast<byte, int>(bytes.Slice(20));
            Span<ulong> valuesetDest = MemoryMarshal.Cast<byte, ulong>(bytes.Slice(24));
            systemDest[0] = systemHash;
            valuesetDest[0] = valuesetHash;

            int codeLength = Math.Min(code.Length, 20);
            for(int i = 0; i < codeLength; i++)
            {
                bytes[i] = (byte)code[i];
            }

            // ~ do one pass of xxHash64
            ulong seed = 0;

            Span<ulong> asUlong = MemoryMarshal.Cast<byte, ulong>(bytes);
            ref ulong first = ref MemoryMarshal.GetReference<ulong>(asUlong);

            Span<ulong> data = stackalloc ulong[4];
            Span<ulong> state = stackalloc ulong[4];

            state[0] = seed + PRIME64_1 + PRIME64_2;
            state[1] = seed + PRIME64_2;
            state[2] = seed;
            state[3] = seed - PRIME64_1;

            data[0] = Unsafe.Add(ref first, 0);
            data[1] = Unsafe.Add(ref first, 1);
            data[2] = Unsafe.Add(ref first, 2);
            data[3] = Unsafe.Add(ref first, 3);

            state[0] = xxHash64_ProcessSingle(state[0], data[0]);
            state[1] = xxHash64_ProcessSingle(state[1], data[1]);
            state[2] = xxHash64_ProcessSingle(state[2], data[2]);
            state[3] = xxHash64_ProcessSingle(state[3], data[3]);

            ulong hash = xxHash64_RotateLeft(state[0], 1) +
                        xxHash64_RotateLeft(state[1], 7) +
                        xxHash64_RotateLeft(state[2], 12) +
                        xxHash64_RotateLeft(state[3], 18);

            hash = (hash ^ xxHash64_ProcessSingle(0, state[0])) * PRIME64_1 + PRIME64_3;
            hash = (hash ^ xxHash64_ProcessSingle(0, state[1])) * PRIME64_1 + PRIME64_3;
            hash = (hash ^ xxHash64_ProcessSingle(0, state[2])) * PRIME64_1 + PRIME64_3;
            hash = (hash ^ xxHash64_ProcessSingle(0, state[3])) * PRIME64_1 + PRIME64_3;

            return hash;
        }

        private readonly Dictionary<ulong, CqlCode> _codesByHash = new();

        private readonly Dictionary<string, HashSet<CqlCode>> _codesInValueSet =
            new(StringComparer.OrdinalIgnoreCase);

        private class CqlCodeHasher : IEqualityComparer<CqlCode>
        {
            public bool Equals(CqlCode? x, CqlCode? y) => true;

            // we're using low ASCII values that are invalid in real codes
            public int GetHashCode(CqlCode obj) =>
                ($"{obj.code ?? "\x1"}\x1").GetHashCode()
                ^ ($"{obj.system ?? "\x2"}\x2").GetHashCode();

        }

    }
}
