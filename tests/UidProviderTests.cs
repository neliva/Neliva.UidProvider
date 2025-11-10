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
            var d = UidProvider.System;

            Assert.IsTrue(object.ReferenceEquals(d, UidProvider.System));

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

            Assert.AreEqual("data", Assert.ThrowsException<ArgumentException>(() => UidProvider.System.Fill(data)).ParamName);
        }

        [TestMethod]
        [DynamicData(nameof(GetInvalidTimeKindTestData), DynamicDataSourceType.Method)]
        public void UidProviderFillBadUtcNowKindFail(DateTime utcNow)
        {
            var data = new byte[16];

            var uid = new TestUidProvider(utcNow, new byte[10]);

            var ex = Assert.ThrowsException<InvalidOperationException>(() => uid.Fill(data));
            
            Assert.AreEqual("The date and time value kind must be UTC.", ex.Message);
        }

        [TestMethod]
        [DynamicData(nameof(GetInvalidUtcNowBeforeUnixTestData), DynamicDataSourceType.Method)]
        public void UidProviderFillBadUtcNowBeforeUnixEpochFail(DateTime utcNow)
        {
            var data = new byte[16];

            var uid = new TestUidProvider(utcNow, new byte[10]);

            var ex = Assert.ThrowsException<InvalidOperationException>(() => uid.Fill(data));
            
            Assert.AreEqual("The date and time value must not be before the Unix epoch.", ex.Message);
        }

        [TestMethod]
        [DynamicData(nameof(GetValidTestData), DynamicDataSourceType.Method)]
        public void UidProviderFillPass(DateTime utcNow, byte[] randPart)
        {
            Span<byte> expectedTime = stackalloc byte[8];
            BinaryPrimitives.WriteUInt64BigEndian(expectedTime, (ulong)((utcNow - DateTime.UnixEpoch).Ticks / TimeSpan.TicksPerMillisecond));
            expectedTime = expectedTime.Slice(2);

            var uid = new TestUidProvider(utcNow, randPart);

            Span<byte> output = stackalloc byte[6 + randPart.Length];
            uid.Fill(output);

            // timestamp
            Assert.IsTrue(MemoryExtensions.SequenceEqual<byte>(expectedTime, output.Slice(0, 6)));
            // random part
            Assert.IsTrue(MemoryExtensions.SequenceEqual<byte>(randPart, output.Slice(6)));

            // Compare to .NET implementation of Unix time milliseconds

            long unixMilliseconds = (long)(BinaryPrimitives.ReadUInt64BigEndian(output) >> 16);

            Assert.AreEqual(new DateTimeOffset(utcNow).ToUnixTimeMilliseconds(), unixMilliseconds);
        }

        [TestMethod]
        public void UidProviderUnixEpochZero()
        {
            var prov = new TestUidProvider(DateTime.UnixEpoch, NewArray(10, 128));

            Span<byte> output = stackalloc byte[16];

            prov.Fill(output);

            Assert.IsTrue(MemoryExtensions.SequenceEqual<byte>(output.Slice(0, 6), new byte[6]));
        }

        private static IEnumerable<object[]> GetValidTestData()
        {
            yield return new object[] { DateTime.UnixEpoch, NewArray(10, 10) };
            yield return new object[] { DateTime.UnixEpoch.AddTicks(1), NewArray(11, 11) };
            yield return new object[] { DateTime.UnixEpoch.AddTicks(101), NewArray(12, 12) };
            yield return new object[] { DateTime.UnixEpoch.AddMilliseconds(1d), NewArray(13, 13) };
            yield return new object[] { DateTime.UnixEpoch.AddSeconds(1), NewArray(14, 14) };
            yield return new object[] { new DateTime(DateTime.MaxValue.AddMicroseconds(-1).Ticks, DateTimeKind.Utc), NewArray(15, 15) };
            yield return new object[] { new DateTime(DateTime.MaxValue.Ticks, DateTimeKind.Utc), NewArray(16, 16) };
            yield return new object[] { new DateTime(2025, 11, 7, 4, 30, 12, 46, 12, DateTimeKind.Utc), NewArray(17, 17) };
            yield return new object[] { DateTime.UnixEpoch.AddMilliseconds(99d), NewArray(18, 18) };
        }

        private static IEnumerable<object[]> GetInvalidTimeKindTestData()
        {
            yield return new object[] { new DateTime(DateTime.UnixEpoch.Ticks, DateTimeKind.Local) };
            yield return new object[] { new DateTime(DateTime.UnixEpoch.Ticks, DateTimeKind.Unspecified) };
            
            yield return new object[] { new DateTime(DateTime.UnixEpoch.Ticks - 1, DateTimeKind.Local) };
            yield return new object[] { new DateTime(DateTime.UnixEpoch.Ticks - 1, DateTimeKind.Unspecified) };
        }

        private static IEnumerable<object[]> GetInvalidUtcNowBeforeUnixTestData()
        {
            yield return new object[] { DateTime.UnixEpoch.AddTicks(-1) };
            yield return new object[] { DateTime.UnixEpoch.AddMilliseconds(-1d) };

        }
        private static byte[] NewArray(int length, byte fillByte)
        {
            var a = length == 0 ? Array.Empty<byte>() : new byte[length];

            Array.Fill(a, fillByte);

            return a;
        }

        private sealed class TestUidProvider : UidProvider
        {
            private readonly DateTime _utcNow;
            private readonly byte[] _randomBytes;

            public TestUidProvider(DateTime utcNow, byte[] randomBytes)
            {
                _utcNow = utcNow;
                _randomBytes = randomBytes;
            }

            protected override DateTime GetUtcNow() => _utcNow;

            protected override void FillRandom(Span<byte> data)
            {
                _randomBytes.CopyTo(data);
            }
        }
    }
}