// This is free and unencumbered software released into the public domain.
// See the UNLICENSE file in the project root for more information.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Neliva.Tests
{
    public class UidProviderTests
    {
        [Fact]
        public void UidProviderSystemIsSingleton()
        {
            var d = UidProvider.System;

            Assert.NotNull(d);
            Assert.Same(d, UidProvider.System);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(15)]
        [InlineData(33)]
        [InlineData(48)]
        public void UidProviderFillBadDataLengthFail(int dataLength)
        {
            var data = new byte[dataLength];

            var ex = Assert.Throws<ArgumentException>(() => UidProvider.System.Fill(data));
            Assert.Equal("data", ex.ParamName);
        }

        [Theory]
        [MemberData(nameof(GetInvalidTimeKindTestData))]
        public void UidProviderFillBadUtcNowKindFail(DateTime utcNow)
        {
            var data = new byte[16];

            var uid = new TestUidProvider(utcNow, new byte[10]);

            var ex = Assert.Throws<InvalidOperationException>(() => uid.Fill(data));

            Assert.Equal("The date and time value kind must be UTC.", ex.Message);
        }

        [Theory]
        [MemberData(nameof(GetInvalidUtcNowBeforeUnixTestData))]
        public void UidProviderFillBadUtcNowBeforeUnixEpochFail(DateTime utcNow)
        {
            var data = new byte[16];

            var uid = new TestUidProvider(utcNow, new byte[10]);

            var ex = Assert.Throws<InvalidOperationException>(() => uid.Fill(data));

            Assert.Equal("The date and time value must not be before the Unix epoch.", ex.Message);
        }

        [Theory]
        [MemberData(nameof(GetValidTestData))]
        public void UidProviderFillPass(DateTime utcNow, byte[] randPart)
        {
            Span<byte> expectedTime = stackalloc byte[8];
            BinaryPrimitives.WriteUInt64BigEndian(expectedTime, (ulong)((utcNow - DateTime.UnixEpoch).Ticks / TimeSpan.TicksPerMillisecond));
            expectedTime = expectedTime.Slice(2);

            var uid = new TestUidProvider(utcNow, randPart);

            Span<byte> output = stackalloc byte[6 + randPart.Length];
            uid.Fill(output);

            // timestamp
            Assert.True(MemoryExtensions.SequenceEqual<byte>(expectedTime, output.Slice(0, 6)));
            // random part
            Assert.True(MemoryExtensions.SequenceEqual<byte>(randPart, output.Slice(6)));

            // Compare to .NET implementation of Unix time milliseconds

            long unixMilliseconds = (long)(BinaryPrimitives.ReadUInt64BigEndian(output) >> 16);

            Assert.Equal(new DateTimeOffset(utcNow).ToUnixTimeMilliseconds(), unixMilliseconds);
        }

        [Fact]
        public void UidProviderUnixEpochZero()
        {
            var prov = new TestUidProvider(DateTime.UnixEpoch, NewArray(10, 128));

            Span<byte> output = stackalloc byte[16];

            prov.Fill(output);

            Assert.True(MemoryExtensions.SequenceEqual<byte>(output.Slice(0, 6), new byte[6]));
        }

        [Theory]
        [InlineData(16)]
        [InlineData(17)]
        [InlineData(23)]
        [InlineData(24)]
        [InlineData(31)]
        [InlineData(32)]
        public void UidProviderFillAllValidLengthsPass(int dataLength)
        {
            var randPart = NewArray(dataLength - 6, 0xAB);
            var utcNow = new DateTime(2025, 1, 2, 3, 4, 5, 6, DateTimeKind.Utc);
            var prov = new TestUidProvider(utcNow, randPart);

            var output = new byte[dataLength];
            prov.Fill(output);

            // random part filled exactly
            Assert.True(MemoryExtensions.SequenceEqual<byte>(randPart, output.AsSpan(6)));

            // timestamp matches
            long unixMs = (long)(BinaryPrimitives.ReadUInt64BigEndian(output) >> 16);
            Assert.Equal(new DateTimeOffset(utcNow).ToUnixTimeMilliseconds(), unixMs);
        }

        [Fact]
        public void UidProviderFillRandomReceivesCorrectLength()
        {
            int observedLength = -1;
            var prov = new LambdaUidProvider(
                () => DateTime.UnixEpoch,
                span => { observedLength = span.Length; });

            for (int len = 16; len <= 32; len++)
            {
                observedLength = -1;
                prov.Fill(new byte[len]);
                Assert.Equal(len - 6, observedLength);
            }
        }

        [Fact]
        public void UidProviderFillDoesNotOverwriteRandomBytes()
        {
            // Random fills bytes 6..end with a known pattern; the timestamp write
            // must only touch bytes 0..5 and leave the random region untouched.
            var randPart = NewArray(26, 0x5A);
            var utcNow = new DateTime(2099, 12, 31, 23, 59, 59, 999, DateTimeKind.Utc);
            var prov = new TestUidProvider(utcNow, randPart);

            var output = new byte[32];
            prov.Fill(output);

            Assert.True(MemoryExtensions.SequenceEqual<byte>(randPart, output.AsSpan(6)));
        }

        [Fact]
        public void UidProviderFillTimestampIsBigEndian48Bit()
        {
            // Pick a timestamp whose bytes are all distinct so endian errors are detectable.
            long ms = 0x0102_0304_0506L;
            var utcNow = DateTime.UnixEpoch.AddMilliseconds(ms);
            var prov = new TestUidProvider(utcNow, NewArray(10, 0));

            var output = new byte[16];
            prov.Fill(output);

            Assert.Equal(0x01, output[0]);
            Assert.Equal(0x02, output[1]);
            Assert.Equal(0x03, output[2]);
            Assert.Equal(0x04, output[3]);
            Assert.Equal(0x05, output[4]);
            Assert.Equal(0x06, output[5]);
        }

        [Fact]
        public void UidProviderFillDateTimeMaxValueTimestampIsEncodedExactly()
        {
            var utcNow = new DateTime(DateTime.MaxValue.Ticks, DateTimeKind.Utc);
            long expectedMs = new DateTimeOffset(utcNow).ToUnixTimeMilliseconds();
            var prov = new TestUidProvider(utcNow, NewArray(10, 0));

            var output = new byte[16];
            prov.Fill(output);

            var actual =
                ((long)output[0] << 40) |
                ((long)output[1] << 32) |
                ((long)output[2] << 24) |
                ((long)output[3] << 16) |
                ((long)output[4] << 8) |
                output[5];

            Assert.Equal(expectedMs, actual);
        }

        [Fact]
        public void UidProviderFillIsLexicographicallyOrderedByTime()
        {
            var t1 = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var t2 = t1.AddMilliseconds(1);
            var t3 = t1.AddDays(365);

            var rand = NewArray(10, 0xFF); // max random for the earlier id
            var rand2 = NewArray(10, 0x00); // min random for the later ids

            var a = new byte[16];
            var b = new byte[16];
            var c = new byte[16];

            new TestUidProvider(t1, rand).Fill(a);
            new TestUidProvider(t2, rand2).Fill(b);
            new TestUidProvider(t3, rand2).Fill(c);

            Assert.True(MemoryExtensions.SequenceCompareTo<byte>(a, b) < 0);
            Assert.True(MemoryExtensions.SequenceCompareTo<byte>(b, c) < 0);
        }

        [Fact]
        public void UidProviderFillDoesNotWriteOutsideSpan()
        {
            var randPart = NewArray(10, 0x77);
            var prov = new TestUidProvider(DateTime.UnixEpoch.AddMilliseconds(1), randPart);

            var buffer = new byte[16 + 4];
            // Sentinel bytes around the slice we will fill.
            buffer[^1] = 0xEE;
            buffer[^2] = 0xEE;
            buffer[^3] = 0xEE;
            buffer[^4] = 0xEE;

            prov.Fill(buffer.AsSpan(0, 16));

            Assert.Equal(0xEE, buffer[^1]);
            Assert.Equal(0xEE, buffer[^2]);
            Assert.Equal(0xEE, buffer[^3]);
            Assert.Equal(0xEE, buffer[^4]);
        }

        [Fact]
        public void UidProviderFillDoesNotWriteOutsideNonZeroOffsetSpan()
        {
            const byte sentinel = 0xEE;
            var randPart = NewArray(10, 0x44);
            var prov = new TestUidProvider(DateTime.UnixEpoch.AddMilliseconds(42), randPart);

            var buffer = new byte[24];
            Array.Fill(buffer, sentinel);

            prov.Fill(buffer.AsSpan(4, 16));

            Assert.All(buffer.AsSpan(0, 4).ToArray(), b => Assert.Equal(sentinel, b));
            Assert.All(buffer.AsSpan(20, 4).ToArray(), b => Assert.Equal(sentinel, b));
            Assert.True(MemoryExtensions.SequenceEqual<byte>(randPart, buffer.AsSpan(10, 10)));
        }

        [Fact]
        public void UidProviderFillPropagatesUtcNowExceptionWithoutMutatingBuffer()
        {
            bool randomCalled = false;
            var expected = new InvalidOperationException("clock failed");
            var prov = new LambdaUidProvider(
                () => throw expected,
                span => { randomCalled = true; });

            const byte sentinel = 0xA5;
            var buffer = new byte[16];
            Array.Fill(buffer, sentinel);

            var actual = Assert.Throws<InvalidOperationException>(() => prov.Fill(buffer));

            Assert.Same(expected, actual);
            Assert.False(randomCalled);
            Assert.All(buffer, b => Assert.Equal(sentinel, b));
        }

        [Fact]
        public void UidProviderFillPropagatesFillRandomExceptionWithoutWritingTimestamp()
        {
            var expected = new InvalidOperationException("random failed");
            var prov = new LambdaUidProvider(
                () => DateTime.UnixEpoch.AddMilliseconds(1),
                span => throw expected);

            const byte sentinel = 0xA5;
            var buffer = new byte[16];
            Array.Fill(buffer, sentinel);

            var actual = Assert.Throws<InvalidOperationException>(() => prov.Fill(buffer));

            Assert.Same(expected, actual);
            Assert.All(buffer, b => Assert.Equal(sentinel, b));
        }

        [Fact]
        public void UidProviderSystemTimestampMatchesUtcNow()
        {
            // Allow a small tolerance to absorb any non-monotonic UtcNow jitter
            // (e.g. NTP corrections) without making the test flaky.
            const long toleranceMs = 1000;

            var before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            Span<byte> id = stackalloc byte[16];
            UidProvider.System.Fill(id);

            var after = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            long ts = (long)(BinaryPrimitives.ReadUInt64BigEndian(id) >> 16);

            Assert.InRange(ts, before - toleranceMs, after + toleranceMs);
        }

        [Fact]
        public void UidProviderSystemFillRandomWritesRandomBytes()
        {
            // Deterministically verify the default FillRandom delegates to a real RNG
            // by calling it directly with a pre-filled sentinel buffer and asserting
            // every byte was overwritten with non-sentinel data on at least one call.
            var prov = new ExposedRandomProvider();

            const byte sentinel = 0xCD;
            Span<byte> a = stackalloc byte[26];
            Span<byte> b = stackalloc byte[26];
            a.Fill(sentinel);
            b.Fill(sentinel);

            prov.CallFillRandom(a);
            prov.CallFillRandom(b);

            // The two independent RNG calls must produce different output.
            Assert.False(MemoryExtensions.SequenceEqual<byte>(a, b));

            // Neither buffer should remain the all-sentinel pattern.
            Assert.False(IsAll(a, sentinel));
            Assert.False(IsAll(b, sentinel));

            static bool IsAll(ReadOnlySpan<byte> s, byte v)
            {
                foreach (var x in s)
                {
                    if (x != v) return false;
                }
                return true;
            }
        }

        private sealed class ExposedRandomProvider : UidProvider
        {
            public void CallFillRandom(Span<byte> data) => FillRandom(data);
        }

        [Fact]
        public void UidProviderFillEmptyArgumentMessage()
        {
            var ex = Assert.Throws<ArgumentException>(() => UidProvider.System.Fill(Array.Empty<byte>()));
            Assert.Equal("data", ex.ParamName);
            Assert.StartsWith("The span must be between 16 and 32 bytes in length.", ex.Message);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(15)]
        [InlineData(33)]
        public void UidProviderFillInvalidLengthDoesNotInvokeHooks(int dataLength)
        {
            bool utcNowCalled = false;
            bool randomCalled = false;

            var prov = new LambdaUidProvider(
                () => { utcNowCalled = true; return DateTime.UnixEpoch; },
                span => { randomCalled = true; });

            var data = new byte[dataLength];

            Assert.Throws<ArgumentException>(() => prov.Fill(data));

            // Length is validated before the time source or random source is touched.
            Assert.False(utcNowCalled);
            Assert.False(randomCalled);
        }

        [Theory]
        [MemberData(nameof(GetInvalidTimeKindTestData))]
        [MemberData(nameof(GetInvalidUtcNowBeforeUnixTestData))]
        public void UidProviderFillInvalidTimeDoesNotFillRandomOrMutateBuffer(DateTime utcNow)
        {
            bool randomCalled = false;

            var prov = new LambdaUidProvider(
                () => utcNow,
                span => { randomCalled = true; });

            const byte sentinel = 0x9C;
            var buffer = new byte[16];
            Array.Fill(buffer, sentinel);

            Assert.Throws<InvalidOperationException>(() => prov.Fill(buffer));

            // Time is validated before FillRandom runs and before any byte is written,
            // so a failed call must leave the caller's buffer completely untouched.
            Assert.False(randomCalled);
            Assert.All(buffer, b => Assert.Equal(sentinel, b));
        }

        [Fact]
        public void UidProviderFillInvokesHooksExactlyOnce()
        {
            int utcNowCount = 0;
            int randomCount = 0;

            var prov = new LambdaUidProvider(
                () => { utcNowCount++; return new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc); },
                span => { randomCount++; });

            prov.Fill(new byte[20]);

            // Exactly one clock read (no skew window) and one random fill per call.
            Assert.Equal(1, utcNowCount);
            Assert.Equal(1, randomCount);
        }

        [Fact]
        public void UidProviderFillSameMillisecondProducesSameTimestamp()
        {
            // Two instants within the same millisecond (differing only by sub-ms ticks)
            // must yield identical 48-bit timestamps.
            var baseTime = DateTime.UnixEpoch.AddSeconds(5);
            var sameMs = baseTime.AddTicks(TimeSpan.TicksPerMillisecond - 1);

            var a = new byte[16];
            var b = new byte[16];

            new TestUidProvider(baseTime, NewArray(10, 1)).Fill(a);
            new TestUidProvider(sameMs, NewArray(10, 2)).Fill(b);

            Assert.True(MemoryExtensions.SequenceEqual<byte>(a.AsSpan(0, 6), b.AsSpan(0, 6)));
        }

        [Fact]
        public void UidProviderSystemConcurrentFillIsThreadSafe()
        {
            const int count = 4096;
            const long toleranceMs = 1000;

            var before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            var ids = new byte[count][];

            // The System singleton is stateless and must be safe for concurrent use.
            Parallel.For(0, count, i =>
            {
                var id = new byte[32];
                UidProvider.System.Fill(id);
                ids[i] = id;
            });

            var after = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            var randomParts = new HashSet<string>(count);

            foreach (var id in ids)
            {
                // No torn timestamp writes under contention.
                long ts = (long)(BinaryPrimitives.ReadUInt64BigEndian(id) >> 16);
                Assert.InRange(ts, before - toleranceMs, after + toleranceMs);

                // No shared-buffer corruption across threads.
                Assert.True(randomParts.Add(Convert.ToHexString(id.AsSpan(6))));
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(15)]
        [InlineData(33)]
        [InlineData(48)]
        public void UidProviderFillWithTimestampBadDataLengthFail(int dataLength)
        {
            var data = new byte[dataLength];

            var ex = Assert.Throws<ArgumentException>(() => UidProvider.System.Fill(data, DateTime.UnixEpoch));
            Assert.Equal("data", ex.ParamName);
        }

        [Theory]
        [MemberData(nameof(GetInvalidTimeKindTestData))]
        public void UidProviderFillWithTimestampBadKindFail(DateTime timestamp)
        {
            var data = new byte[16];

            var ex = Assert.Throws<ArgumentException>(() => UidProvider.System.Fill(data, timestamp));

            Assert.Equal("timestamp", ex.ParamName);
            Assert.StartsWith("The date and time value kind must be UTC.", ex.Message);
        }

        [Theory]
        [MemberData(nameof(GetInvalidUtcNowBeforeUnixTestData))]
        public void UidProviderFillWithTimestampBeforeUnixEpochFail(DateTime timestamp)
        {
            var data = new byte[16];

            var ex = Assert.Throws<ArgumentException>(() => UidProvider.System.Fill(data, timestamp));

            Assert.Equal("timestamp", ex.ParamName);
            Assert.StartsWith("The date and time value must not be before the Unix epoch.", ex.Message);
        }

        [Theory]
        [MemberData(nameof(GetValidTestData))]
        public void UidProviderFillWithTimestampPass(DateTime timestamp, byte[] randPart)
        {
            Span<byte> expectedTime = stackalloc byte[8];
            BinaryPrimitives.WriteUInt64BigEndian(expectedTime, (ulong)((timestamp - DateTime.UnixEpoch).Ticks / TimeSpan.TicksPerMillisecond));
            expectedTime = expectedTime.Slice(2);

            // The clock hook is left at its default (unused by this overload).
            var uid = new TestUidProvider(default, randPart);

            Span<byte> output = stackalloc byte[6 + randPart.Length];
            uid.Fill(output, timestamp);

            // timestamp
            Assert.True(MemoryExtensions.SequenceEqual<byte>(expectedTime, output.Slice(0, 6)));
            // random part
            Assert.True(MemoryExtensions.SequenceEqual<byte>(randPart, output.Slice(6)));

            long unixMilliseconds = (long)(BinaryPrimitives.ReadUInt64BigEndian(output) >> 16);

            Assert.Equal(new DateTimeOffset(timestamp).ToUnixTimeMilliseconds(), unixMilliseconds);
        }

        [Fact]
        public void UidProviderFillWithTimestampDoesNotInvokeGetUtcNow()
        {
            bool utcNowCalled = false;

            var prov = new LambdaUidProvider(
                () => { utcNowCalled = true; return DateTime.UnixEpoch; },
                span => { });

            prov.Fill(new byte[16], new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc));

            // The explicit-timestamp overload must never consult the clock hook.
            Assert.False(utcNowCalled);
        }

        [Fact]
        public void UidProviderFillWithTimestampInvalidLengthDoesNotFillRandom()
        {
            bool randomCalled = false;

            var prov = new LambdaUidProvider(
                () => DateTime.UnixEpoch,
                span => { randomCalled = true; });

            Assert.Throws<ArgumentException>(() => prov.Fill(new byte[15], DateTime.UnixEpoch));

            // Length is validated before the random source is touched.
            Assert.False(randomCalled);
        }

        [Theory]
        [MemberData(nameof(GetInvalidTimeKindTestData))]
        [MemberData(nameof(GetInvalidUtcNowBeforeUnixTestData))]
        public void UidProviderFillWithTimestampInvalidTimeDoesNotFillRandomOrMutateBuffer(DateTime timestamp)
        {
            bool randomCalled = false;

            var prov = new LambdaUidProvider(
                () => DateTime.UnixEpoch,
                span => { randomCalled = true; });

            const byte sentinel = 0x9C;
            var buffer = new byte[16];
            Array.Fill(buffer, sentinel);

            Assert.Throws<ArgumentException>(() => prov.Fill(buffer, timestamp));

            // A rejected timestamp must leave the caller's buffer completely untouched.
            Assert.False(randomCalled);
            Assert.All(buffer, b => Assert.Equal(sentinel, b));
        }

        [Fact]
        public void UidProviderFillWithTimestampMatchesParameterlessTimestamp()
        {
            var utc = new DateTime(2025, 3, 4, 5, 6, 7, 8, DateTimeKind.Utc);
            var rand = NewArray(10, 0x33);

            var viaHook = new byte[16];
            new TestUidProvider(utc, rand).Fill(viaHook);

            var viaArgument = new byte[16];
            new TestUidProvider(utc, rand).Fill(viaArgument, utc);

            // Both overloads must encode the same instant identically.
            Assert.True(MemoryExtensions.SequenceEqual<byte>(viaHook.AsSpan(0, 6), viaArgument.AsSpan(0, 6)));
        }

        public static IEnumerable<object[]> GetValidTestData()
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

        public static IEnumerable<object[]> GetInvalidTimeKindTestData()
        {
            yield return new object[] { new DateTime(DateTime.UnixEpoch.Ticks, DateTimeKind.Local) };
            yield return new object[] { new DateTime(DateTime.UnixEpoch.Ticks, DateTimeKind.Unspecified) };

            yield return new object[] { new DateTime(DateTime.UnixEpoch.Ticks - 1, DateTimeKind.Local) };
            yield return new object[] { new DateTime(DateTime.UnixEpoch.Ticks - 1, DateTimeKind.Unspecified) };
        }

        public static IEnumerable<object[]> GetInvalidUtcNowBeforeUnixTestData()
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

        private sealed class LambdaUidProvider : UidProvider
        {
            private readonly Func<DateTime> _utcNow;
            private readonly SpanAction _fillRandom;

            public delegate void SpanAction(Span<byte> data);

            public LambdaUidProvider(Func<DateTime> utcNow, SpanAction fillRandom)
            {
                _utcNow = utcNow;
                _fillRandom = fillRandom;
            }

            protected override DateTime GetUtcNow() => _utcNow();

            protected override void FillRandom(Span<byte> data) => _fillRandom(data);
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
