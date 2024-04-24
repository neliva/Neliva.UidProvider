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
        public void UidProviderFillPass(byte[] node, DateTime utcNow, uint counter, byte[] rand)
        {
            int i = 0;

            var uid = new UidProvider(
                node,
                new UidUtcNowFunc(() => utcNow),
                new UidRngFillAction((data) =>
                {
                    if (i++ == 0)
                    {
                        BinaryPrimitives.WriteUInt32BigEndian(data, counter);
                    }
                }));

            Span<byte> rd = stackalloc byte[16];
            Span<byte> tbytes = stackalloc byte[8];

            uid.Fill(rd);

            long timestampMs = (utcNow - DateTime.UnixEpoch).Ticks / TimeSpan.TicksPerMillisecond;

            BinaryPrimitives.WriteUInt64BigEndian(tbytes, (ulong)timestampMs);

            Assert.IsTrue(MemoryExtensions.SequenceEqual<byte>(rd.Slice(0, 6), tbytes.Slice(2, 6)));

            uint fillCount = BinaryPrimitives.ReadUInt32BigEndian(rd.Slice(12));

            Assert.IsTrue(MemoryExtensions.SequenceEqual<byte>(node, rd.Slice(6, 6)));

            Assert.AreEqual(counter + 1, fillCount);
        }

        private static IEnumerable<object[]> GetValidTestData()
        {
            yield return new object[]
            {
                new byte[6],
                DateTime.UnixEpoch,
                0u,
                new byte[0],
            };
        }
    }
}