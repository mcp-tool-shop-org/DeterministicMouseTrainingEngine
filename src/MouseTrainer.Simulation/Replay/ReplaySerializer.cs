using System.Text;
using MouseTrainer.Domain.Runs;
using MouseTrainer.Domain.Utility;

namespace MouseTrainer.Simulation.Replay;

/// <summary>
/// Binary serialization for ReplayEnvelope.
/// Wire format: [Header][RunDescriptorSection][InputTraceSection][VerificationSection][Checksum]
/// Little-endian throughout. Unsigned LEB128 for variable-length integers.
/// File extension: .mtr
/// </summary>
public static class ReplaySerializer
{
    private static readonly byte[] Magic = { (byte)'M', (byte)'T', (byte)'R', (byte)'P' };

    /// <summary>
    /// Serialize a ReplayEnvelope to a binary stream.
    /// </summary>
    public static void Write(ReplayEnvelope envelope, Stream output)
    {
        using var buffer = new MemoryStream();
        using var w = new BinaryWriter(buffer, Encoding.UTF8, leaveOpen: true);

        // ── Header (8 bytes) ──
        w.Write(Magic);
        w.Write(envelope.FormatVersion);   // uint16 LE
        w.Write((ushort)0);                // Flags, reserved

        // ── RunDescriptor Section (length-prefixed) ──
        WriteSectionWithLength(w, buffer, () =>
        {
            WriteString(w, envelope.Mode.Value);
            w.Write(envelope.Seed);                                     // uint32 LE
            w.Write((byte)envelope.Difficulty);                         // uint8
            w.Write(ValidateUInt16(envelope.GeneratorVersion, "GeneratorVersion")); // uint16 LE
            w.Write(ValidateUInt16(envelope.RulesetVersion, "RulesetVersion"));     // uint16 LE
            w.Write(ValidateUInt16(envelope.FixedHz, "FixedHz"));                   // uint16 LE

            Leb128.WriteUInt32(w, (uint)envelope.Mutators.Count);

            foreach (var spec in envelope.Mutators)
            {
                WriteString(w, spec.Id.Value);
                w.Write(ValidateUInt16(spec.Version, "MutatorVersion")); // uint16 LE

                Leb128.WriteUInt32(w, (uint)spec.Params.Count);
                foreach (var param in spec.Params)
                {
                    WriteString(w, param.Key);
                    w.Write(param.Value); // float32 IEEE 754 LE
                }
            }
        });

        // ── InputTrace Section (length-prefixed) ──
        WriteSectionWithLength(w, buffer, () =>
        {
            w.Write((uint)envelope.Trace.TotalTicks); // uint32 LE
            Leb128.WriteUInt32(w, (uint)envelope.Trace.Spans.Count);

            foreach (var span in envelope.Trace.Spans)
            {
                Leb128.WriteUInt32(w, (uint)span.DurationTicks);
                w.Write(span.Sample.X);       // int16 LE
                w.Write(span.Sample.Y);       // int16 LE
                w.Write(span.Sample.Buttons); // uint8
            }
        });

        // ── Verification Section (24 bytes fixed) ──
        w.Write(envelope.FinalScore);       // int32 LE
        w.Write(envelope.FinalMaxCombo);    // int32 LE
        w.Write(envelope.Hash.Value);       // uint64 LE

        w.Flush();

        // ── Checksum (8 bytes) ──
        var payload = buffer.GetBuffer();
        int payloadLength = (int)buffer.Length;
        ulong checksum = ComputeChecksum(payload, payloadLength);
        w.Write(checksum); // uint64 LE
        w.Flush();

        // Copy to output
        buffer.Position = 0;
        buffer.CopyTo(output);
    }

    /// <summary>
    /// Deserialize a ReplayEnvelope from a binary stream.
    /// Validates magic, version, checksum, and structural integrity.
    /// </summary>
    public static ReplayEnvelope Read(Stream input)
    {
        // Read entire stream for checksum verification
        byte[] data;
        if (input is MemoryStream ms && ms.Position == 0)
        {
            data = ms.ToArray();
        }
        else
        {
            using var copy = new MemoryStream();
            input.CopyTo(copy);
            data = copy.ToArray();
        }

        if (data.Length < 48) // 8 header + 4 + 4 + 24 + 8
            throw new InvalidDataException("Replay file too short.");

        // ── Verify magic ──
        if (data[0] != 'M' || data[1] != 'T' || data[2] != 'R' || data[3] != 'P')
            throw new InvalidDataException("Invalid replay file: bad magic.");

        using var reader = new BinaryReader(new MemoryStream(data), Encoding.UTF8, leaveOpen: false);

        // ── Header ──
        reader.ReadBytes(4); // skip magic (already verified)
        ushort formatVersion = reader.ReadUInt16();
        if (formatVersion != ReplayEnvelope.CurrentFormatVersion)
            throw new InvalidDataException($"Unsupported replay format version: {formatVersion}.");
        ushort flags = reader.ReadUInt16(); // reserved, ignored

        // ── Verify checksum (over all bytes before the final 8) ──
        int payloadLength = data.Length - 8;
        ulong storedChecksum = BitConverter.ToUInt64(data, payloadLength);
        ulong computedChecksum = ComputeChecksum(data, payloadLength);
        if (storedChecksum != computedChecksum)
            throw new InvalidDataException("Replay file corrupted: checksum mismatch.");

        // ── RunDescriptor Section ──
        uint runSectionLength = reader.ReadUInt32();
        long runSectionEnd = reader.BaseStream.Position + runSectionLength;

        string modeValue = ReadString(reader);
        if (string.IsNullOrEmpty(modeValue))
            throw new InvalidDataException("ModeId must not be empty.");

        uint seed = reader.ReadUInt32();
        byte diffByte = reader.ReadByte();
        ushort genVer = reader.ReadUInt16();
        ushort ruleVer = reader.ReadUInt16();
        ushort fixedHz = reader.ReadUInt16();

        uint mutatorCount = Leb128.ReadUInt32(reader);
        var mutators = new List<MutatorSpec>((int)mutatorCount);

        for (uint m = 0; m < mutatorCount; m++)
        {
            string mutIdValue = ReadString(reader);
            if (string.IsNullOrEmpty(mutIdValue))
                throw new InvalidDataException("MutatorId must not be empty.");

            ushort mutVersion = reader.ReadUInt16();

            uint paramCount = Leb128.ReadUInt32(reader);
            var parms = new List<MutatorParam>((int)paramCount);

            for (uint p = 0; p < paramCount; p++)
            {
                string key = ReadString(reader);
                float value = reader.ReadSingle();
                parms.Add(new MutatorParam(key, value));
            }

            // Validate param sort order
            for (int i = 1; i < parms.Count; i++)
            {
                if (string.Compare(parms[i].Key, parms[i - 1].Key, StringComparison.Ordinal) <= 0)
                    throw new InvalidDataException("Mutator params not in sorted order.");
            }

            mutators.Add(MutatorSpec.Create(new MutatorId(mutIdValue), mutVersion, parms));
        }

        // ── InputTrace Section ──
        uint traceSectionLength = reader.ReadUInt32();

        uint totalTicks = reader.ReadUInt32();
        uint spanCount = Leb128.ReadUInt32(reader);
        var spans = new List<InputSpan>((int)spanCount);
        uint tickSum = 0;

        for (uint s = 0; s < spanCount; s++)
        {
            uint duration = Leb128.ReadUInt32(reader);
            short xq = reader.ReadInt16();
            short yq = reader.ReadInt16();
            byte buttons = reader.ReadByte();

            spans.Add(new InputSpan((int)duration, new InputSample(xq, yq, buttons)));
            tickSum += duration;
        }

        if (tickSum != totalTicks)
            throw new InvalidDataException(
                $"TotalTicks mismatch: header says {totalTicks}, spans sum to {tickSum}.");

        // ── Verification Section ──
        int finalScore = reader.ReadInt32();
        int finalMaxCombo = reader.ReadInt32();
        ulong eventHash = reader.ReadUInt64();

        // ── Reconstruct RunDescriptor (recomputes RunId) ──
        var run = RunDescriptor.Create(
            new ModeId(modeValue),
            seed,
            (DifficultyTier)diffByte,
            genVer,
            ruleVer,
            mutators.Count > 0 ? mutators : null);

        return new ReplayEnvelope
        {
            FormatVersion = formatVersion,
            RunId = run.Id,
            Mode = run.Mode,
            Seed = run.Seed,
            Difficulty = run.Difficulty,
            GeneratorVersion = run.GeneratorVersion,
            RulesetVersion = run.RulesetVersion,
            Mutators = run.Mutators,
            FixedHz = fixedHz,
            Trace = InputTrace.FromSpans(spans),
            Hash = new VerificationHash(eventHash),
            FinalScore = finalScore,
            FinalMaxCombo = finalMaxCombo,
        };
    }

    // ── Private helpers ──

    private static void WriteSectionWithLength(BinaryWriter w, MemoryStream buffer, Action writePayload)
    {
        long lengthPos = buffer.Position;
        w.Write((uint)0); // placeholder

        writePayload();
        w.Flush();

        long endPos = buffer.Position;
        uint sectionLength = (uint)(endPos - lengthPos - 4);

        buffer.Position = lengthPos;
        w.Write(sectionLength);
        buffer.Position = endPos;
    }

    private static void WriteString(BinaryWriter w, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        Leb128.WriteUInt32(w, (uint)bytes.Length);
        w.Write(bytes);
    }

    private static string ReadString(BinaryReader reader)
    {
        uint length = Leb128.ReadUInt32(reader);
        var bytes = reader.ReadBytes((int)length);
        if (bytes.Length != (int)length)
            throw new EndOfStreamException("Unexpected end of stream reading string.");
        return Encoding.UTF8.GetString(bytes);
    }

    private static ushort ValidateUInt16(int value, string name)
    {
        if (value < 0 || value > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(name,
                $"{name} value {value} exceeds uint16 range [0, {ushort.MaxValue}].");
        return (ushort)value;
    }

    private static ulong ComputeChecksum(byte[] data, int length)
    {
        ulong hash = Fnv1a.OffsetBasis;
        for (int i = 0; i < length; i++)
            hash = Fnv1a.HashByte(hash, data[i]);
        return hash;
    }
}
