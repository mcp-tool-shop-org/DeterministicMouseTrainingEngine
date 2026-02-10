using System.IO;

namespace MouseTrainer.Domain.Utility;

/// <summary>
/// Unsigned LEB128 (Little-Endian Base 128) varint encoding.
/// Protobuf-style: 7 payload bits per byte, MSB is continuation flag.
/// Used by the replay binary format for variable-length integers.
/// </summary>
public static class Leb128
{
    /// <summary>
    /// Encode an unsigned 32-bit integer as LEB128 into a BinaryWriter.
    /// </summary>
    public static void WriteUInt32(BinaryWriter writer, uint value)
    {
        do
        {
            byte b = (byte)(value & 0x7F);
            value >>= 7;
            if (value != 0)
                b |= 0x80;
            writer.Write(b);
        } while (value != 0);
    }

    /// <summary>
    /// Decode an unsigned 32-bit LEB128 integer from a BinaryReader.
    /// Throws InvalidDataException if the encoding exceeds 5 bytes.
    /// Throws EndOfStreamException if the stream ends prematurely.
    /// </summary>
    public static uint ReadUInt32(BinaryReader reader)
    {
        uint result = 0;
        int shift = 0;

        for (int i = 0; i < 5; i++)
        {
            byte b = reader.ReadByte();
            result |= (uint)(b & 0x7F) << shift;
            if ((b & 0x80) == 0)
                return result;
            shift += 7;
        }

        throw new InvalidDataException("Varint overflow: exceeded 5 bytes for uint32.");
    }
}
