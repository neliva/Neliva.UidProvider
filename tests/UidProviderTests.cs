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
        public void UidProviderDefaultPass()
        {
            var d = UidProvider.Default;

            Assert.IsTrue(object.ReferenceEquals(d, UidProvider.Default));

            Span<byte> id1 = stackalloc byte[32];
            Span<byte> id2 = stackalloc byte[32];

            d.Fill(id1);
            d.Fill(id2);

            // random part
            Assert.IsFalse(MemoryExtensions.SequenceEqual<byte>(id1.Slice(6), id2.Slice(6)));

            // timestamp
            var t1 = BinaryPrimitives.ReadUInt64BigEndian(id1) >> 16;
            var t2 = BinaryPrimitives.ReadUInt64BigEndian(id2) >> 16;
            Assert.IsTrue((t2 - t1) < 2000); // two calls should not be more than 2s apart.
        }

        [TestMethod]
        [DataRow(0)]
        [DataRow(1)]
        [DataRow(15)]
        [DataRow(33)]
        [DataRow(48)]
        public void UidProviderFillBadDataLengthFail(int dataLength)
        {
            var data = new byte[dataLength];

            Assert.AreEqual("data", Assert.ThrowsException<ArgumentException>(() => UidProvider.Default.Fill(data)).ParamName);
        }

        [TestMethod]
        [DynamicData(nameof(GetInvalidTimeKindTestData), DynamicDataSourceType.Method)]
        public void UidProviderFillBadUtcNowKindFail(DateTime utcNow)
        {
            var data = new byte[16];

            var uid = new UidProvider(utcNow: () => utcNow);

            var ex = Assert.ThrowsException<ArgumentException>(() => uid.Fill(data));
            
            Assert.AreEqual("The date and time value kind must be UTC.", ex.Message);
        }

        [TestMethod]
        public void UidProviderFillBadUtcNowBeforeUnixEpochFail()
        {
            var data = new byte[16];

            var utcNow = DateTime.UnixEpoch.AddMilliseconds(-1d);

            var uid = new UidProvider(utcNow: () => utcNow);

            var ex = Assert.ThrowsException<ArgumentException>(() => uid.Fill(data));
            
            Assert.AreEqual("The date and time value must not be before the Unix epoch.", ex.Message);
        }

        [TestMethod]
        [DynamicData(nameof(GetValidTestData), DynamicDataSourceType.Method)]
        public void UidProviderFillPass(DateTime utcNow, byte[] randPart)
        {
            Span<byte> expectedTime = stackalloc byte[8];
            BinaryPrimitives.WriteUInt64BigEndian(expectedTime, (ulong)((utcNow - DateTime.UnixEpoch).Ticks / TimeSpan.TicksPerMillisecond));
            expectedTime = expectedTime.Slice(2);

            var uid = new UidProvider(
                utcNow: () => utcNow,
                rngFill: (data) =>
                {
                    Assert.AreEqual(randPart.Length, data.Length);
                    new Span<byte>(randPart).CopyTo(data);
                });

            Span<byte> output = stackalloc byte[6 + randPart.Length];
            uid.Fill(output);

            // timestamp
            Assert.IsTrue(MemoryExtensions.SequenceEqual<byte>(expectedTime, output.Slice(0, 6)));
            // random part
            Assert.IsTrue(MemoryExtensions.SequenceEqual<byte>(randPart, output.Slice(6)));
        }

        private static IEnumerable<object[]> GetValidTestData()
        {
            var maxDate = new DateTime(DateTime.MaxValue.Ticks, DateTimeKind.Utc);

            yield return new object[] { DateTime.UnixEpoch, NewArray(10, 0) };
            yield return new object[] { DateTime.UnixEpoch.AddMilliseconds(1d), NewArray(26, 0xAB) };
            yield return new object[] { maxDate, NewArray(16, 0xFF) };
        }

        private static byte[] NewArray(int length, byte fillByte)
        {
            var a = length == 0 ? Array.Empty<byte>() : new byte[length];
            
            Array.Fill(a, fillByte);

            return a;
        }

        private static IEnumerable<object[]> GetInvalidTimeKindTestData()
        {
            yield return new object[] { new DateTime(DateTime.UnixEpoch.Ticks, DateTimeKind.Local) };
            yield return new object[] { new DateTime(DateTime.UnixEpoch.Ticks, DateTimeKind.Unspecified) };
            
            yield return new object[] { new DateTime(DateTime.UnixEpoch.Ticks - 1, DateTimeKind.Local) };
            yield return new object[] { new DateTime(DateTime.UnixEpoch.Ticks - 1, DateTimeKind.Unspecified) };
        }
    }
}