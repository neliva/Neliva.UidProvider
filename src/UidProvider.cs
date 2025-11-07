// This is free and unencumbered software released into the public domain.
// See the UNLICENSE file in the project root for more information.

using System;
using System.Security.Cryptography;

namespace Neliva
{
    /// <summary>
    /// Generates unique, time-ordered identifiers (UIDs) of variable length.
    /// </summary>
    /// <remarks>
    /// Each UID is between 16 and 32 bytes. The first 6 bytes contain a 48-bit, big-endian
    /// timestamp representing milliseconds since the Unix epoch. The remaining 10–26 bytes
    /// are filled with cryptographically strong random data.
    /// <para>
    /// The resulting identifiers are lexicographically sortable by creation time when encoded
    /// in hexadecimal or base32hex formats.
    /// </para>
    /// <para>
    /// Byte layout:
    /// <code>
    /// Bytes 0..5  : 48-bit timestamp (big-endian), milliseconds since Unix epoch.
    /// Bytes 6..31 : Cryptographically strong random bytes.
    /// </code>
    /// </para>
    /// </remarks>
    public abstract class UidProvider
    {
        /// <summary>
        /// Gets the system <see cref="UidProvider"/> implementation, which uses
        /// <see cref="DateTime.UtcNow"/> for time and <see cref="RandomNumberGenerator.Fill(Span{byte})"/>
        /// for cryptographically strong randomness.
        /// </summary>
        public static UidProvider System { get; } = new SystemUidProvider();

        /// <summary>
        /// Initializes a new instance of the <see cref="UidProvider"/> class.
        /// </summary>
        protected UidProvider()
        {
        }

        /// <summary>
        /// Returns the current UTC timestamp used for UID generation.
        /// Override to supply a custom time source (e.g. a cached or simulated clock).
        /// </summary>
        protected virtual DateTime GetUtcNow() => DateTime.UtcNow;

        /// <summary>
        /// Fills the provided span with cryptographically strong random bytes.
        /// Override to customize the randomness source.
        /// </summary>
        protected virtual void FillRandom(Span<byte> data) => RandomNumberGenerator.Fill(data);

        /// <summary>
        /// Fills the specified span with a generated unique identifier.
        /// </summary>
        /// <param name="data">
        /// A span whose length must be between 16 and 32 bytes.
        /// </param>
        /// <exception cref="ArgumentException">
        /// The <paramref name="data"/> length is not between 16 and 32 (inclusive).
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// The resolved UTC time is not <see cref="DateTimeKind.Utc"/> or is before
        /// <see cref="DateTime.UnixEpoch"/>.
        /// </exception>
        /// <remarks>
        /// The resulting identifier is lexicographically sortable by timestamp.
        /// </remarks>
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
        /// System default provider implementation.
        /// </summary>
        private sealed class SystemUidProvider : UidProvider
        {
        }
    }
}