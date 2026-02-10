using MouseTrainer.Domain.Utility;
using Xunit;

namespace MouseTrainer.Tests.Utility;

public class Leb128Tests
{
    // ── 1. Single byte: 0 ──

    [Fact]
    public void Encode_Zero_SingleByte()
    {
        var bytes = Encode(0);
        Assert.Equal(new byte[] { 0x00 }, bytes);
    }

    // ── 2. Single byte: 127 ──

    [Fact]
    public void Encode_127_SingleByte()
    {
        var bytes = Encode(127);
        Assert.Equal(new byte[] { 0x7F }, bytes);
    }

    // ── 3. Two bytes: 128 ──

    [Fact]
    public void Encode_128_TwoBytes()
    {
        var bytes = Encode(128);
        Assert.Equal(new byte[] { 0x80, 0x01 }, bytes);
    }

    // ── 4. Five bytes: uint.MaxValue ──

    [Fact]
    public void Encode_UInt32Max_FiveBytes()
    {
        var bytes = Encode(uint.MaxValue);
        Assert.Equal(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x0F }, bytes);
    }

    // ── 5. Round-trip for boundary values ──

    [Theory]
    [InlineData(0u)]
    [InlineData(1u)]
    [InlineData(127u)]
    [InlineData(128u)]
    [InlineData(16383u)]
    [InlineData(16384u)]
    [InlineData(2097151u)]
    [InlineData(2097152u)]
    [InlineData(uint.MaxValue)]
    public void RoundTrip_BoundaryValues(uint value)
    {
        var bytes = Encode(value);
        var decoded = Decode(bytes);
        Assert.Equal(value, decoded);
    }

    // ── 6. Round-trip for powers of 2 ──

    [Fact]
    public void RoundTrip_AllPowersOfTwo()
    {
        for (int i = 0; i < 32; i++)
        {
            uint value = 1u << i;
            var bytes = Encode(value);
            var decoded = Decode(bytes);
            Assert.Equal(value, decoded);
        }
    }

    // ── 7. Golden vectors: known byte sequences ──

    [Theory]
    [InlineData(new byte[] { 0x00 }, 0u)]
    [InlineData(new byte[] { 0x01 }, 1u)]
    [InlineData(new byte[] { 0x7F }, 127u)]
    [InlineData(new byte[] { 0x80, 0x01 }, 128u)]
    [InlineData(new byte[] { 0xAC, 0x02 }, 300u)]
    [InlineData(new byte[] { 0xFF, 0xFF, 0x03 }, 65535u)]
    public void GoldenVectors_KnownByteSequences(byte[] input, uint expected)
    {
        var decoded = Decode(input);
        Assert.Equal(expected, decoded);
    }

    // ── 8. Overflow: 6+ continuation bytes ──

    [Fact]
    public void Decode_Overflow_Throws()
    {
        // 6 bytes all with continuation bit set
        var bytes = new byte[] { 0x80, 0x80, 0x80, 0x80, 0x80, 0x01 };
        Assert.Throws<InvalidDataException>(() => Decode(bytes));
    }

    // ── 9. Truncated stream ──

    [Fact]
    public void Decode_TruncatedStream_Throws()
    {
        // Stream ends with continuation bit set
        var bytes = new byte[] { 0x80 };
        Assert.Throws<EndOfStreamException>(() => Decode(bytes));
    }

    // ── Helpers ──

    private static byte[] Encode(uint value)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        Leb128.WriteUInt32(writer, value);
        writer.Flush();
        return ms.ToArray();
    }

    private static uint Decode(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        using var reader = new BinaryReader(ms);
        return Leb128.ReadUInt32(reader);
    }
}
