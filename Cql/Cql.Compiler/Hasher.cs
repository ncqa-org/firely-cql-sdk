/* 
 * Copyright (c) 2023, NCQA and contributors
 * See the file CONTRIBUTORS for details.
 * 
 * This file is licensed under the BSD 3-Clause license
 * available at https://raw.githubusercontent.com/FirelyTeam/firely-cql-sdk/main/LICENSE
 */

using System;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace Hl7.Cql.Compiler
{
    internal class Hasher
    {

        public virtual string Hash(string input)
        {
            var bytes = Encoding.UTF8.GetBytes(input);
            // SHA-256 is FIPS-compliant (MD5 is not); truncated to 16 bytes to keep identifiers short.
            using var sha = SHA256.Create();
            var hashBytes = sha.ComputeHash(bytes);
            Array.Resize(ref hashBytes, 16);
            var alpha = ToAlphaString(hashBytes);
            return alpha;
        }

        public string ToAlphaString(byte[] toConvert, bool bigEndian = false)
        {
            const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
            if (bigEndian) Array.Reverse(toConvert); // !BitConverter.IsLittleEndian might be an alternative
            BigInteger dividend = new(toConvert);
            var builder = new StringBuilder();
            while (dividend != 0)
            {
                BigInteger remainder;
                dividend = BigInteger.DivRem(dividend, 36, out remainder);
                builder.Insert(0, alphabet[Math.Abs(((int)remainder))]);
            }
            return builder.ToString();
        }

    }
}
