using MouseTrainer.Domain.Events;
using MouseTrainer.Domain.Input;
using MouseTrainer.Domain.Runs;
using MouseTrainer.Simulation.Core;
using MouseTrainer.Simulation.Modes.ReflexGates;
using MouseTrainer.Simulation.Replay;
using MouseTrainer.Simulation.Session;
using Xunit;

namespace MouseTrainer.Tests.Replay;

public class ReplaySerializerTests
{
    private const int FixedHz = 60;
    private const float Dt = 1f / FixedHz;
    private const uint TestSeed = 0xC0FFEEu;

    private static readonly PointerInput CenterInput =
        new(960f, 540f, false, false, 0L);

    // ══════════════════════════════════════════
    //  Round-trip tests
    // ══════════════════════════════════════════

    // ── 1. Minimal envelope (no mutators, single span) ──

    [Fact]
    public void RoundTrip_MinimalEnvelope_Identity()
    {
        var run = RunDescriptor.Create(new ModeId("ReflexGates"), TestSeed);
        var sample = InputSample.Quantize(960f, 540f, false, false);
        var trace = InputTrace.FromTickSamples(new List<InputSample> { sample, sample, sample });

        var original = new ReplayEnvelope
        {
            FormatVersion = ReplayEnvelope.CurrentFormatVersion,
            RunId = run.Id,
            Mode = run.Mode,
            Seed = run.Seed,
            Difficulty = run.Difficulty,
            GeneratorVersion = run.GeneratorVersion,
            RulesetVersion = run.RulesetVersion,
            Mutators = run.Mutators,
            FixedHz = 60,
            Trace = trace,
            Hash = new VerificationHash(0xDEADBEEFUL),
            FinalScore = 0,
            FinalMaxCombo = 0,
        };

        var deserialized = WriteAndRead(original);

        AssertEnvelopesEqual(original, deserialized);
    }

    // ── 2. Full envelope with mutators and params ──

    [Fact]
    public void RoundTrip_FullEnvelope_WithMutators_Identity()
    {
        var mutators = new List<MutatorSpec>
        {
            MutatorSpec.Create(MutatorId.NarrowMargin, 1, new List<MutatorParam>
            {
                new("factor", 0.8f),
            }),
            MutatorSpec.Create(MutatorId.DifficultyCurve, 1, new List<MutatorParam>
            {
                new("curve", 1.5f),
                new("floor", 0.3f),
            }),
        };

        var run = RunDescriptor.Create(
            new ModeId("ReflexGates"), 42u, DifficultyTier.Standard, 1, 1, mutators);

        var samples = new List<InputSample>
        {
            InputSample.Quantize(100f, 200f, false, false),
            InputSample.Quantize(100f, 200f, false, false),
            InputSample.Quantize(300f, 400f, true, false),
            InputSample.Quantize(500f, 600f, false, true),
        };
        var trace = InputTrace.FromTickSamples(samples);

        var original = new ReplayEnvelope
        {
            FormatVersion = ReplayEnvelope.CurrentFormatVersion,
            RunId = run.Id,
            Mode = run.Mode,
            Seed = run.Seed,
            Difficulty = run.Difficulty,
            GeneratorVersion = run.GeneratorVersion,
            RulesetVersion = run.RulesetVersion,
            Mutators = run.Mutators,
            FixedHz = 60,
            Trace = trace,
            Hash = new VerificationHash(0x123456789ABCDEF0UL),
            FinalScore = 1234,
            FinalMaxCombo = 5,
        };

        var deserialized = WriteAndRead(original);

        AssertEnvelopesEqual(original, deserialized);
    }

    // ── 3. Real session → serialize → deserialize → verify ──

    [Fact]
    public void RoundTrip_RealSession_VerifierPasses()
    {
        var run = RunDescriptor.Create(new ModeId("ReflexGates"), TestSeed);
        var envelope = RecordFullSession(run, CenterInput);

        var deserialized = WriteAndRead(envelope);

        var freshSim = new ReflexGateSimulation();
        var verification = ReplayVerifier.Verify(deserialized, freshSim);

        Assert.True(verification.IsValid,
            $"Hash: {verification.ExpectedHash} vs {verification.ActualHash}; " +
            $"Score: {verification.ExpectedScore} vs {verification.ActualScore}");
    }

    // ── 4. Write → Read → Write → byte-identical ──

    [Fact]
    public void WriteReadWrite_ByteIdentical()
    {
        var run = RunDescriptor.Create(new ModeId("ReflexGates"), TestSeed);
        var envelope = RecordFullSession(run, CenterInput);

        var bytes1 = SerializeToBytes(envelope);
        var deserialized = Deserialize(bytes1);
        var bytes2 = SerializeToBytes(deserialized);

        Assert.Equal(bytes1, bytes2);
    }

    // ══════════════════════════════════════════
    //  Corruption / tamper tests
    // ══════════════════════════════════════════

    // ── 5. Flip bytes → checksum failure ──

    [Fact]
    public void TamperBytes_ChecksumFailure()
    {
        var run = RunDescriptor.Create(new ModeId("ReflexGates"), TestSeed);
        var envelope = RecordFullSession(run, CenterInput);
        var bytes = SerializeToBytes(envelope);

        // Test representative positions: skip magic (0-3) and version (4-5) — those
        // trigger "bad magic" or "unsupported version" before checksum verification.
        // All other byte positions should trigger checksum failure.
        int[] positions = { 8, 12, 20, bytes.Length / 2, bytes.Length - 10 };

        foreach (int pos in positions)
        {
            var tampered = (byte[])bytes.Clone();
            tampered[pos] ^= 0xFF; // flip all bits

            var ex = Assert.Throws<InvalidDataException>(() =>
                Deserialize(tampered));
            Assert.Contains("checksum mismatch", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ── 6. Tamper checksum itself ──

    [Fact]
    public void TamperChecksum_Detected()
    {
        var run = RunDescriptor.Create(new ModeId("ReflexGates"), TestSeed);
        var envelope = RecordFullSession(run, CenterInput);
        var bytes = SerializeToBytes(envelope);

        // Corrupt the last 8 bytes (checksum)
        bytes[^1] ^= 0xFF;

        var ex = Assert.Throws<InvalidDataException>(() => Deserialize(bytes));
        Assert.Contains("checksum mismatch", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── 7. Invalid magic ──

    [Fact]
    public void InvalidMagic_Throws()
    {
        var run = RunDescriptor.Create(new ModeId("ReflexGates"), TestSeed);
        var envelope = RecordFullSession(run, CenterInput);
        var bytes = SerializeToBytes(envelope);

        bytes[0] = (byte)'X';
        bytes[1] = (byte)'X';
        bytes[2] = (byte)'X';
        bytes[3] = (byte)'X';

        var ex = Assert.Throws<InvalidDataException>(() => Deserialize(bytes));
        Assert.Contains("bad magic", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── 8. Unsupported version ──

    [Fact]
    public void UnsupportedVersion_Throws()
    {
        var run = RunDescriptor.Create(new ModeId("ReflexGates"), TestSeed);
        var envelope = RecordFullSession(run, CenterInput);
        var bytes = SerializeToBytes(envelope);

        // Overwrite version (bytes 4-5) with 99, then recompute checksum
        bytes[4] = 99;
        bytes[5] = 0;
        RecomputeChecksum(bytes);

        var ex = Assert.Throws<InvalidDataException>(() => Deserialize(bytes));
        Assert.Contains("Unsupported replay format version", ex.Message);
    }

    // ══════════════════════════════════════════
    //  Edge case tests
    // ══════════════════════════════════════════

    // ── 9. Empty mutators round-trip ──

    [Fact]
    public void EmptyMutators_RoundTrip()
    {
        var run = RunDescriptor.Create(new ModeId("ReflexGates"), 42u);
        var sample = InputSample.Quantize(0f, 0f, false, false);
        var trace = InputTrace.FromTickSamples(new List<InputSample> { sample });

        var original = new ReplayEnvelope
        {
            FormatVersion = ReplayEnvelope.CurrentFormatVersion,
            RunId = run.Id,
            Mode = run.Mode,
            Seed = run.Seed,
            Difficulty = run.Difficulty,
            GeneratorVersion = run.GeneratorVersion,
            RulesetVersion = run.RulesetVersion,
            Mutators = run.Mutators,
            FixedHz = 60,
            Trace = trace,
            Hash = new VerificationHash(0UL),
            FinalScore = 0,
            FinalMaxCombo = 0,
        };

        var deserialized = WriteAndRead(original);

        Assert.Empty(deserialized.Mutators);
    }

    // ── 10. Empty trace round-trip ──

    [Fact]
    public void EmptyTrace_RoundTrip()
    {
        var run = RunDescriptor.Create(new ModeId("ReflexGates"), 42u);
        var trace = InputTrace.FromTickSamples(new List<InputSample>());

        var original = new ReplayEnvelope
        {
            FormatVersion = ReplayEnvelope.CurrentFormatVersion,
            RunId = run.Id,
            Mode = run.Mode,
            Seed = run.Seed,
            Difficulty = run.Difficulty,
            GeneratorVersion = run.GeneratorVersion,
            RulesetVersion = run.RulesetVersion,
            Mutators = run.Mutators,
            FixedHz = 60,
            Trace = trace,
            Hash = new VerificationHash(0UL),
            FinalScore = 0,
            FinalMaxCombo = 0,
        };

        var deserialized = WriteAndRead(original);

        Assert.Equal(0, deserialized.Trace.TotalTicks);
        Assert.Empty(deserialized.Trace.Spans);
    }

    // ── 11. TotalTicks mismatch ──

    [Fact]
    public void TotalTicks_Mismatch_Throws()
    {
        var run = RunDescriptor.Create(new ModeId("ReflexGates"), 42u);
        var sample = InputSample.Quantize(0f, 0f, false, false);
        var trace = InputTrace.FromTickSamples(new List<InputSample> { sample, sample, sample });

        var envelope = new ReplayEnvelope
        {
            FormatVersion = ReplayEnvelope.CurrentFormatVersion,
            RunId = run.Id,
            Mode = run.Mode,
            Seed = run.Seed,
            Difficulty = run.Difficulty,
            GeneratorVersion = run.GeneratorVersion,
            RulesetVersion = run.RulesetVersion,
            Mutators = run.Mutators,
            FixedHz = 60,
            Trace = trace,
            Hash = new VerificationHash(0UL),
            FinalScore = 0,
            FinalMaxCombo = 0,
        };

        var bytes = SerializeToBytes(envelope);

        // Find and tamper TotalTicks in the InputTrace section
        // After header(8) + run section, there's trace section length(4) then TotalTicks(4)
        // Instead of finding the exact offset, let's tamper the TotalTicks to a wrong value
        // by modifying the bytes and recomputing checksum.
        // We know TotalTicks=3 is at some offset. Let's find it by reading the run section length.
        int runSectionLenOffset = 8;
        uint runSectionLen = BitConverter.ToUInt32(bytes, runSectionLenOffset);
        int traceSectionLenOffset = runSectionLenOffset + 4 + (int)runSectionLen;
        int totalTicksOffset = traceSectionLenOffset + 4; // after trace section length

        // Change TotalTicks from 3 to 999
        BitConverter.GetBytes((uint)999).CopyTo(bytes, totalTicksOffset);
        RecomputeChecksum(bytes);

        var ex = Assert.Throws<InvalidDataException>(() => Deserialize(bytes));
        Assert.Contains("TotalTicks mismatch", ex.Message);
    }

    // ── 12. Truncated stream ──

    [Fact]
    public void TruncatedStream_Throws()
    {
        var run = RunDescriptor.Create(new ModeId("ReflexGates"), TestSeed);
        var envelope = RecordFullSession(run, CenterInput);
        var bytes = SerializeToBytes(envelope);

        // Truncate at various points
        Assert.ThrowsAny<Exception>(() => Deserialize(bytes[..10])); // mid-header
        Assert.ThrowsAny<Exception>(() => Deserialize(bytes[..20])); // mid-section
    }

    // ── 13. File too short ──

    [Fact]
    public void TooShort_Throws()
    {
        var ex = Assert.Throws<InvalidDataException>(() =>
            Deserialize(new byte[] { (byte)'M', (byte)'T', (byte)'R', (byte)'P' }));
        Assert.Contains("too short", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ══════════════════════════════════════════
    //  Golden vector
    // ══════════════════════════════════════════

    // ── 14. Golden byte size for known minimal envelope ──

    [Fact]
    public void GoldenByteSize_MinimalEnvelope()
    {
        var run = RunDescriptor.Create(new ModeId("ReflexGates"), TestSeed);
        var sample = InputSample.Quantize(960f, 540f, false, false);
        var trace = InputTrace.FromTickSamples(new List<InputSample> { sample });

        var envelope = new ReplayEnvelope
        {
            FormatVersion = ReplayEnvelope.CurrentFormatVersion,
            RunId = run.Id,
            Mode = run.Mode,
            Seed = run.Seed,
            Difficulty = run.Difficulty,
            GeneratorVersion = run.GeneratorVersion,
            RulesetVersion = run.RulesetVersion,
            Mutators = run.Mutators,
            FixedHz = 60,
            Trace = trace,
            Hash = new VerificationHash(0UL),
            FinalScore = 0,
            FinalMaxCombo = 0,
        };

        var bytes = SerializeToBytes(envelope);

        // Frozen golden bytes — regression firewall for wire format
        Assert.Equal(75, bytes.Length);

        // Verify exact byte sequence (frozen)
        var expectedHex = "4D54525001000000180000000B5265666C65784761746573EEFFC00000010001003C00000B000000010000000101802518150000000000000000000000000000000000432BAB8D35BD3F0B";
        var actualHex = BitConverter.ToString(bytes).Replace("-", "");
        Assert.Equal(expectedHex, actualHex);

        // Write → Read round-trip still works
        var deserialized = Deserialize(bytes);
        AssertEnvelopesEqual(envelope, deserialized);
    }

    // ══════════════════════════════════════════
    //  Helpers
    // ══════════════════════════════════════════

    private static ReplayEnvelope WriteAndRead(ReplayEnvelope envelope)
    {
        var bytes = SerializeToBytes(envelope);
        return Deserialize(bytes);
    }

    private static byte[] SerializeToBytes(ReplayEnvelope envelope)
    {
        using var ms = new MemoryStream();
        ReplaySerializer.Write(envelope, ms);
        return ms.ToArray();
    }

    private static ReplayEnvelope Deserialize(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        return ReplaySerializer.Read(ms);
    }

    private static void RecomputeChecksum(byte[] bytes)
    {
        int payloadLength = bytes.Length - 8;
        ulong hash = MouseTrainer.Domain.Utility.Fnv1a.OffsetBasis;
        for (int i = 0; i < payloadLength; i++)
            hash = MouseTrainer.Domain.Utility.Fnv1a.HashByte(hash, bytes[i]);
        BitConverter.GetBytes(hash).CopyTo(bytes, payloadLength);
    }

    private static void AssertEnvelopesEqual(ReplayEnvelope expected, ReplayEnvelope actual)
    {
        Assert.Equal(expected.FormatVersion, actual.FormatVersion);
        Assert.Equal(expected.RunId, actual.RunId);
        Assert.Equal(expected.Mode, actual.Mode);
        Assert.Equal(expected.Seed, actual.Seed);
        Assert.Equal(expected.Difficulty, actual.Difficulty);
        Assert.Equal(expected.GeneratorVersion, actual.GeneratorVersion);
        Assert.Equal(expected.RulesetVersion, actual.RulesetVersion);
        Assert.Equal(expected.FixedHz, actual.FixedHz);
        Assert.Equal(expected.Mutators.Count, actual.Mutators.Count);

        for (int i = 0; i < expected.Mutators.Count; i++)
        {
            Assert.Equal(expected.Mutators[i].Id, actual.Mutators[i].Id);
            Assert.Equal(expected.Mutators[i].Version, actual.Mutators[i].Version);
            Assert.Equal(expected.Mutators[i].Params.Count, actual.Mutators[i].Params.Count);
            for (int p = 0; p < expected.Mutators[i].Params.Count; p++)
            {
                Assert.Equal(expected.Mutators[i].Params[p].Key, actual.Mutators[i].Params[p].Key);
                Assert.Equal(expected.Mutators[i].Params[p].Value, actual.Mutators[i].Params[p].Value);
            }
        }

        Assert.Equal(expected.Trace.TotalTicks, actual.Trace.TotalTicks);
        Assert.Equal(expected.Trace.Spans.Count, actual.Trace.Spans.Count);
        for (int i = 0; i < expected.Trace.Spans.Count; i++)
        {
            Assert.Equal(expected.Trace.Spans[i].DurationTicks, actual.Trace.Spans[i].DurationTicks);
            Assert.Equal(expected.Trace.Spans[i].Sample, actual.Trace.Spans[i].Sample);
        }

        Assert.Equal(expected.Hash, actual.Hash);
        Assert.Equal(expected.FinalScore, actual.FinalScore);
        Assert.Equal(expected.FinalMaxCombo, actual.FinalMaxCombo);
    }

    private static ReplayEnvelope RecordFullSession(RunDescriptor run, PointerInput input)
    {
        var sim = new ReflexGateSimulation();
        sim.Reset(run.Seed);

        var recorder = new ReplayRecorder();
        var sc = new SessionController();
        sc.ResetToReady(run, sim.Gates.Count);
        sc.Start();

        var frameEvents = new List<GameEvent>(capacity: 16);
        bool complete = false;

        for (int tick = 0; tick < 8000 && !complete; tick++)
        {
            recorder.RecordTick(input);

            frameEvents.Clear();
            sim.FixedUpdate(tick, Dt, input, frameEvents);

            complete = sc.ApplyEvents(frameEvents);
        }

        var result = sc.GetResult()!;
        return recorder.Finalize(run, FixedHz, result, result.EventHash!.Value);
    }
}
