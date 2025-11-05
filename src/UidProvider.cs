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
    public sealed class UidProvider
    {
        /// <summary>
        /// Gets a default implementation of the <see cref="UidProvider"/> class that relies on
        /// the <see cref="DateTime.UtcNow"/> and the <see cref="RandomNumberGenerator.Fill(Span{byte})"/>
        /// to generate unique IDs.
        /// </summary>
        public static UidProvider Default { get; } = new UidProvider();

        private readonly UidUtcNowFunc utcNowFunc;
        private readonly UidRngFillAction rngFillAction;

        /// <summary>
        /// Initializes a new instance of the <see cref="UidProvider"/> class.
        /// </summary>
        /// <param name="utcNow">
        /// A callback that provides the current UTC date and time
        /// to embed in the unique identifier.
        /// </param>
        /// <param name="rngFill">
        /// A callback that fills a span with
        /// cryptographically strong random bytes.
        /// </param>
        public UidProvider(UidUtcNowFunc utcNow = default, UidRngFillAction rngFill = default)
        {
            this.utcNowFunc = utcNow ?? new UidUtcNowFunc(static () => DateTime.UtcNow);
            this.rngFillAction = rngFill ?? new UidRngFillAction(RandomNumberGenerator.Fill);
        }

        /// <summary>
        /// Fills the span with the unique identifier bytes.
        /// </summary>
        /// <param name="data">
        /// The span to fill with the unique identifier bytes.
        /// </param>
        /// <exception cref="ArgumentException">
        /// The <paramref name="data"/> span must be between 16 and 32 bytes in length.
        /// - or -
        /// The <see cref="UidUtcNowFunc"/> callback value kind is not <see cref="DateTimeKind.Utc"/>,
        /// or the value is before the <see cref="DateTime.UnixEpoch"/> value.
        /// </exception>
        public void Fill(Span<byte> data)
        {
            if (data.Length < 16 || data.Length > 32)
            {
                throw new ArgumentException("The span must be between 16 and 32 bytes in length.", nameof(data));
            }

            DateTime utcNow = this.utcNowFunc();

            if (utcNow.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException("The date and time value kind must be UTC.");
            }

            if (utcNow < DateTime.UnixEpoch)
            {
                throw new ArgumentException("The date and time value must not be before the Unix epoch.");
            }

            long timestamp = (utcNow - DateTime.UnixEpoch).Ticks / TimeSpan.TicksPerMillisecond;

            this.rngFillAction(data.Slice(6));

            data[5] = (byte)timestamp;
            data[4] = (byte)(timestamp >> 8);
            data[3] = (byte)(timestamp >> 16);
            data[2] = (byte)(timestamp >> 24);
            data[1] = (byte)(timestamp >> 32);
            data[0] = (byte)(timestamp >> 40);                
        }
    }

    /// <summary>
    /// Encapsulates a method that returns a <see cref="DateTime"/> object
    /// that is set to the current UTC date and time on this computer.
    /// </summary>
    /// <returns>
    /// An object whose value is the current UTC local date and time.
    /// </returns>
    /// <seealso cref="UidProvider"/>
    public delegate DateTime UidUtcNowFunc();

    /// <summary>
    /// Encapsulates a method that fills a span with
    /// cryptographically strong random bytes.
    /// </summary>
    /// <param name="data">
    /// The span to fill with cryptographically strong random bytes.
    /// </param>
    /// <seealso cref="UidProvider"/>
    public delegate void UidRngFillAction(Span<byte> data);
}