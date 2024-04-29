// This is free and unencumbered software released into the public domain.
// See the UNLICENSE file in the project root for more information.

using System;
using System.Security.Cryptography;
using System.Threading;

namespace Neliva
{
    /// <summary>
    /// Provides functionality for generating unique across space and time identifiers.
    /// </summary>
    /// <remarks>
    /// <code>
    /// +-------------+--------+-----------+----------+
    /// |  Timestamp  |  Node  |  Counter  |  Random  |
    /// +-------------+--------+-----------+----------+
    /// |  6          |  6     |  4        |  0 - 16  |
    /// +-------------+--------+-----------+----------+ 
    /// </code>
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

        private readonly byte node5;
        private readonly byte node4;
        private readonly byte node3;
        private readonly byte node2;
        private readonly byte node1;
        private readonly byte node0;

        private uint counterRef;

        /// <summary>
        /// Initializes a new instance of the <see cref="UidProvider"/> class.
        /// </summary>
        /// <param name="node">
        /// A 6 byte node value to embed in the unique identifier.
        /// If not provided, a random value will be chosen.</param>
        /// <param name="utcNow">
        /// A callback that provides the current UTC date and time
        /// to embed in the unique identifier.
        /// </param>
        /// <param name="rngFill">
        /// A callback that fills a span with
        /// cryptographically strong random bytes.
        /// </param>
        /// <exception cref="ArgumentException">
        /// The <paramref name="node"/> span must be 6 bytes in length.
        /// </exception>
        public UidProvider(ReadOnlySpan<byte> node = default, UidUtcNowFunc utcNow = default, UidRngFillAction rngFill = default)
        {
            if (node.Length != 0 && node.Length != 6)
            {
                throw new ArgumentException("The span must be 6 bytes in length.", nameof(node));
            }

            this.utcNowFunc = utcNow ?? new UidUtcNowFunc(static () => DateTime.UtcNow);
            this.rngFillAction = rngFill ?? new UidRngFillAction(static (data) => RandomNumberGenerator.Fill(data));

            Span<byte> rd = stackalloc byte[16];

            this.rngFillAction(rd);

            this.counterRef =
                  ((uint)rd[15]) |
                  ((uint)rd[14] << 8) |
                  ((uint)rd[13] << 16) |
                  ((uint)rd[12] << 24);

            var useNode = node.IsEmpty ? rd.Slice(2, 6) : node;

            this.node5 = useNode[5];
            this.node4 = useNode[4];
            this.node3 = useNode[3];
            this.node2 = useNode[2];
            this.node1 = useNode[1];
            this.node0 = useNode[0];
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

            if (data.Length > 16)
            {
                this.rngFillAction(data.Slice(16));
            }

            uint counter = Interlocked.Increment(ref this.counterRef);

            data[15] = (byte)counter;
            data[14] = (byte)(counter >> 8);
            data[13] = (byte)(counter >> 16);
            data[12] = (byte)(counter >> 24);

            data[11] = node5;
            data[10] = node4;
            data[9] = node3;
            data[8] = node2;
            data[7] = node1;
            data[6] = node0;

            data[5] = (byte)timestamp;
            data[4] = (byte)(timestamp >> 8);
            data[3] = (byte)(timestamp >> 16);
            data[2] = (byte)(timestamp >> 24);
            data[1] = (byte)(timestamp >> 32);
            data[0] = (byte)(timestamp >> 40);
        }
    }
}