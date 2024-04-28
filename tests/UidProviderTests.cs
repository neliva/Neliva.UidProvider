// This is free and unencumbered software released into the public domain.
// See the UNLICENSE file in the project root for more information.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq.Expressions;
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
        public void UidProviderFillPass(ulong? node, ulong randNode, DateTime utcNow, ulong counter, byte[] randPart)
        {
            bool isInitialized = false;

            Span<byte> expectedNode = stackalloc byte[8];
            BinaryPrimitives.WriteUInt64BigEndian(expectedNode, node.HasValue ? node.Value : randNode);
            expectedNode = expectedNode.Slice(2);

            Span<byte> expectedTime = stackalloc byte[8];
            BinaryPrimitives.WriteUInt64BigEndian(expectedTime, (ulong)((utcNow - DateTime.UnixEpoch).Ticks / TimeSpan.TicksPerMillisecond));
            expectedTime = expectedTime.Slice(2);

            uint expectedCounter = (uint)counter + 1;

            var uid = new UidProvider(
                node.HasValue ? expectedNode : Span<byte>.Empty,
                new UidUtcNowFunc(() => utcNow),
                new UidRngFillAction((data) =>
                {
                    if (!isInitialized) // Constructor()
                    {
                        Assert.AreEqual(16, data.Length);

                        BinaryPrimitives.WriteUInt64BigEndian(data.Slice(0), randNode);
                        BinaryPrimitives.WriteUInt64BigEndian(data.Slice(8), counter);

                        isInitialized = true;
                    }
                    else // Fill()
                    {
                        Assert.AreEqual(randPart.Length, data.Length);

                        new Span<byte>(randPart).CopyTo(data);
                    }
                }));

            Span<byte> output = stackalloc byte[16 + randPart.Length];

            uid.Fill(output);

            // timestamp
            Assert.IsTrue(MemoryExtensions.SequenceEqual<byte>(expectedTime, output.Slice(0, 6)));

            // node
            Assert.IsTrue(MemoryExtensions.SequenceEqual<byte>(expectedNode, output.Slice(6, 6)));

            // counter - internally already incremented
            Assert.AreEqual(expectedCounter, BinaryPrimitives.ReadUInt32BigEndian(output.Slice(12, 4)));

            // random part
            Assert.IsTrue(MemoryExtensions.SequenceEqual<byte>(randPart, output.Slice(16)));
        }

        private static IEnumerable<object[]> GetValidTestData()
        {
            // ulong node, ulong randNode, DateTime utcNow, ulong counter, byte[] randPart

            yield return new object[]
            {
                null,
                0ul,
                DateTime.UnixEpoch,
                0ul,
                NewArray(0, 0),
            };

            yield return new object[]
            {
                0ul,
                0ul,
                DateTime.UnixEpoch,
                (ulong)uint.MaxValue << 32,
                NewArray(0, 0),
            };

            yield return new object[]
            {
                0ul,
                0ul,
                DateTime.UnixEpoch,
                1ul,
                NewArray(0, 0),
            };

            yield return new object[]
            {
                0ul,
                0ul,
                DateTime.UnixEpoch,
                (ulong)uint.MaxValue - 1,
                NewArray(0, 0),
            };

            yield return new object[]
            {
                0ul,
                0ul,
                DateTime.UnixEpoch,
                (ulong)uint.MaxValue,
                NewArray(0, 0),
            };

            yield return new object[]
            {
                0ul,
                0ul,
                DateTime.UnixEpoch,
                ulong.MaxValue,
                NewArray(0, 0),
            };

            yield return new object[]
            {
                0ul,
                0ul,
                DateTime.UnixEpoch,
                0xff00ff00ff00ff00ul,
                NewArray(0, 0),
            };

            yield return new object[]
            {
                0ul,
                0ul,
                DateTime.UnixEpoch,
                0x1122334455667788ul,
                NewArray(0, 0),
            };
        }

        private static byte[] NewArray(int length, byte fillByte)
        {
            var a = new byte[length];

            Array.Fill(a, fillByte);

            return a;
        }
    }
}