// This is free and unencumbered software released into the public domain.
// See the UNLICENSE file in the project root for more information.

using System;
using System.Security.Cryptography;

namespace Neliva
{
    /// <summary>
    /// Provides functionality for generating unique time ordered identifiers.
    /// </summary>
    /// <remarks>
    /// The ID consists of a 6 byte timestamp and 10-26 random bytes.
    /// </remarks>
    public abstract class UidProvider
    {
        /// <summary>
        /// Gets a <see cref="UidProvider"/> instance that relies on
        /// the <see cref="DateTime.UtcNow"/> and the <see cref="RandomNumberGenerator.Fill(Span{byte})"/>
        /// to generate unique identifiers.
        /// </summary>
        public static UidProvider Default { get; } = new DefaultUidProvider();

        /// <summary>
        /// Initializes a new instance of the <see cref="UidProvider"/> class.
        /// </summary>
        protected UidProvider()
        {
        }

        /// <summary>
        /// Returns the current UTC date and time on this computer.
        /// </summary>
        protected virtual DateTime GetUtcNow() => DateTime.UtcNow;

        /// <summary>
        /// Fills a span with cryptographically strong random bytes.
        /// </summary>
        protected virtual void FillRandom(Span<byte> data) => RandomNumberGenerator.Fill(data);

        /// <summary>
        /// Fills the span with the unique identifier bytes.
        /// </summary>
        /// <param name="data">
        /// The span to fill with the unique identifier bytes.
        /// </param>
        /// <exception cref="ArgumentException">
        /// The <paramref name="data"/> span must be between 16 and 32 bytes in length.
        /// </exception>
        public void Fill(Span<byte> data)
        {
            if (data.Length < 16 || data.Length > 32)
            {
                throw new ArgumentException("The span must be between 16 and 32 bytes in length.", nameof(data));
            }

            DateTime utcNow = this.GetUtcNow();

            if (utcNow.Kind != DateTimeKind.Utc)
            {
                throw new InvalidOperationException("The date and time value kind must be UTC.");
            }

            if (utcNow < DateTime.UnixEpoch)
            {
                throw new InvalidOperationException("The date and time value must not be before the Unix epoch.");
            }

            long timestamp = (utcNow - DateTime.UnixEpoch).Ticks / TimeSpan.TicksPerMillisecond;

            this.FillRandom(data.Slice(6));

            data[5] = (byte)timestamp;
            data[4] = (byte)(timestamp >> 8);
            data[3] = (byte)(timestamp >> 16);
            data[2] = (byte)(timestamp >> 24);
            data[1] = (byte)(timestamp >> 32);
            data[0] = (byte)(timestamp >> 40);
        }

        /// <summary>
        /// The default provider implementation.
        /// </summary>
        private sealed class DefaultUidProvider : UidProvider
        {
        }
    }
}