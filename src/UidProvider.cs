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
        /// The resolved UTC time is not <see cref="DateTimeKind.Utc"/> or
        /// is before <see cref="DateTime.UnixEpoch"/>.
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

            DateTime timestamp = this.GetUtcNow();

            if (timestamp.Kind != DateTimeKind.Utc)
            {
                throw new InvalidOperationException("The date and time value kind must be UTC.");
            }

            if (timestamp < DateTime.UnixEpoch)
            {
                throw new InvalidOperationException("The date and time value must not be before the Unix epoch.");
            }

            this.FillCore(data, timestamp);
        }

        /// <summary>
        /// Fills the specified span with a generated unique identifier that encodes the
        /// supplied UTC <paramref name="timestamp"/> instead of the current UTC time.
        /// </summary>
        /// <param name="data">
        /// A span whose length must be between 16 and 32 bytes.
        /// </param>
        /// <param name="timestamp">
        /// The timestamp to encode. Its <see cref="DateTime.Kind"/> must be
        /// <see cref="DateTimeKind.Utc"/> and the value must not be before
        /// <see cref="DateTime.UnixEpoch"/>.
        /// </param>
        /// <exception cref="ArgumentException">
        /// The <paramref name="data"/> length is not between 16 and 32 (inclusive), or
        /// <paramref name="timestamp"/> is not <see cref="DateTimeKind.Utc"/>, or
        /// <paramref name="timestamp"/> is before <see cref="DateTime.UnixEpoch"/>.
        /// </exception>
        /// <remarks>
        /// The resulting identifier is lexicographically sortable by timestamp.
        /// </remarks>
        public void Fill(Span<byte> data, DateTime timestamp)
        {
            if (data.Length < 16 || data.Length > 32)
            {
                throw new ArgumentException("The span must be between 16 and 32 bytes in length.", nameof(data));
            }

            if (timestamp.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException("The date and time value kind must be UTC.", nameof(timestamp));
            }

            if (timestamp < DateTime.UnixEpoch)
            {
                throw new ArgumentException("The date and time value must not be before the Unix epoch.", nameof(timestamp));
            }

            this.FillCore(data, timestamp);
        }

        private void FillCore(Span<byte> data, DateTime timestamp)
        {
            const long unixEpochTicks = 621355968000000000L;
            const long unixEpochMilliseconds = unixEpochTicks / TimeSpan.TicksPerMillisecond;

            long ms = (timestamp.Ticks / TimeSpan.TicksPerMillisecond) - unixEpochMilliseconds;

            this.FillRandom(data.Slice(6));

            data[5] = (byte)ms;
            data[4] = (byte)(ms >> 8);
            data[3] = (byte)(ms >> 16);
            data[2] = (byte)(ms >> 24);
            data[1] = (byte)(ms >> 32);
            data[0] = (byte)(ms >> 40);
        }

        /// <summary>
        /// System default provider implementation.
        /// </summary>
        private sealed class SystemUidProvider : UidProvider
        {
        }
    }
}
