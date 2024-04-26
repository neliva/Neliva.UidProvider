// This is free and unencumbered software released into the public domain.
// See the UNLICENSE file in the project root for more information.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Neliva.Tests
{
    [TestClass]
    public class UidProviderTests
    {
        [TestMethod]
        [DataRow(1)]
        [DataRow(2)]
        [DataRow(3)]
        [DataRow(4)]
        [DataRow(5)]
        [DataRow(7)]
        [DataRow(8)]
        public void UidProviderCreateWithBadNodeFail(int nodeLength)
        {
            Assert.AreEqual("node", Assert.ThrowsException<ArgumentException>(() => new UidProvider(new byte[nodeLength])).ParamName);
        }

        [TestMethod]
        [DataRow(0)]
        [DataRow(6)]
        public void UidProviderCreateWithNodePass(int nodeLength)
        {
            new UidProvider(new byte[nodeLength]);
        }

        [TestMethod]
        [DynamicData(nameof(GetValidTestData), DynamicDataSourceType.Method)]
        public void UidProviderFillPass(byte[] node, DateTime utcNow, ulong counter, byte[] rand)
        {
            int i = 0;

            var uid = new UidProvider(
                node,
                new UidUtcNowFunc(() => utcNow),
                new UidRngFillAction((data) =>
                {
                    if (i++ == 0)
                    {
                        // Constructor initialize counter
                        BinaryPrimitives.WriteUInt64BigEndian(data.Slice(8), counter);
                    }
                }));

            Span<byte> rd = stackalloc byte[16 + rand.Length];
            Span<byte> tbytes = stackalloc byte[8];

            uid.Fill(rd);

            long timestampMs = (utcNow - DateTime.UnixEpoch).Ticks / TimeSpan.TicksPerMillisecond;

            BinaryPrimitives.WriteUInt64BigEndian(tbytes, (ulong)timestampMs);

            // timestamp
            Assert.IsTrue(MemoryExtensions.SequenceEqual<byte>(tbytes.Slice(2, 6), rd.Slice(0, 6)));

            // node
            Assert.IsTrue(MemoryExtensions.SequenceEqual<byte>(node, rd.Slice(6, 6)));

            // counter - internally already incremented
            uint fillCount = BinaryPrimitives.ReadUInt32BigEndian(rd.Slice(12));

            uint expectedCounter = (uint)counter + 1;

            Assert.AreEqual(expectedCounter, fillCount);

            // random part
            Assert.IsTrue(MemoryExtensions.SequenceEqual<byte>(rand, rd.Slice(16)));
        }

        private static IEnumerable<object[]> GetValidTestData()
        {
            // byte[] node, DateTime utcNow, ulong counter, byte[] rand

            yield return new object[]
            {
                new byte[6],
                DateTime.UnixEpoch,
                (ulong)0,
                new byte[0],
            };

            yield return new object[]
            {
                new byte[6],
                DateTime.UnixEpoch,
                (ulong)uint.MaxValue << 32,
                new byte[0],
            };

            yield return new object[]
            {
                new byte[6],
                DateTime.UnixEpoch,
                (ulong)1,
                new byte[0],
            };

            yield return new object[]
            {
                new byte[6],
                DateTime.UnixEpoch,
                (ulong)uint.MaxValue - 1,
                new byte[0],
            };

            yield return new object[]
            {
                new byte[6],
                DateTime.UnixEpoch,
                (ulong)uint.MaxValue,
                new byte[0],
            };

            yield return new object[]
            {
                new byte[6],
                DateTime.UnixEpoch,
                (ulong)ulong.MaxValue,
                new byte[0],
            };

            yield return new object[]
            {
                new byte[6],
                DateTime.UnixEpoch,
                (ulong)0xff00ff00ff00ff00,
                new byte[0],
            };

            yield return new object[]
            {
                new byte[6],
                DateTime.UnixEpoch,
                (ulong)0x1122334455667788,
                new byte[0],
            };
        }
    }
}