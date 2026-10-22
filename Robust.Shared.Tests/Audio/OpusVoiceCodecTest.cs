using System;
using NUnit.Framework;
using Robust.Shared.Audio.Codecs;

namespace Robust.Shared.Tests.Audio;

[TestFixture, Parallelizable, TestOf(typeof(OpusVoiceEncoder))]
internal sealed class OpusVoiceCodecTest
{
    private const int Rate = 48000;
    private const int Frame = 960; // 20 ms

    private static short[] Sine(int length, float freq, int offset = 0)
    {
        var data = new short[length];
        for (var i = 0; i < length; i++)
        {
            data[i] = (short) (MathF.Sin(2 * MathF.PI * freq * (i + offset) / Rate) * 8000);
        }

        return data;
    }

    [Test]
    public void RoundTrip()
    {
        var enc = new OpusVoiceEncoder(Rate) { Bitrate = 32000, UseInbandFec = true };
        var dec = new OpusVoiceDecoder(Rate);
        var packet = new byte[1275];
        var output = new short[Frame];

        double energy = 0;
        for (var f = 0; f < 10; f++)
        {
            var len = enc.Encode(Sine(Frame, 440, f * Frame), packet);
            Assert.That(len, Is.GreaterThan(0));
            Assert.That(OpusPacket.GetSampleCount(packet.AsSpan(0, len), Rate), Is.EqualTo(Frame));

            var decoded = dec.Decode(packet.AsSpan(0, len), output);
            Assert.That(decoded, Is.EqualTo(Frame));

            if (f > 3)
            {
                foreach (var s in output)
                {
                    energy += s * (double) s;
                }
            }
        }

        // Codec warmed up and is producing a signal of roughly the right amplitude.
        var rms = Math.Sqrt(energy / (6 * Frame));
        Assert.That(rms, Is.InRange(3000, 9000));

        Assert.That(dec.DecodeLost(output), Is.EqualTo(Frame));
    }

    [Test]
    public void RejectsGarbage()
    {
        var dec = new OpusVoiceDecoder(Rate);
        var output = new short[Frame];

        Assert.That(dec.Decode(ReadOnlySpan<byte>.Empty, output), Is.EqualTo(-1));

        // Code 3 packet claiming 63 frames of 20 ms: way over the 120 ms limit.
        Assert.That(OpusPacket.GetSampleCount(new byte[] { 0xFB, 0x3F, 0, 0 }, Rate), Is.EqualTo(-1));
        Assert.That(dec.Decode(new byte[] { 0xFB, 0x3F, 0, 0 }, output), Is.EqualTo(-1));

        // Random bytes must never throw.
        var rng = new System.Random(1234);
        var junk = new byte[200];
        for (var i = 0; i < 500; i++)
        {
            rng.NextBytes(junk);
            Assert.DoesNotThrow(() => dec.Decode(junk.AsSpan(0, rng.Next(1, junk.Length)), output));
        }
    }

    [Test]
    [TestCase((byte) 0x08, 960)]  // SILK 20 ms, 1 frame
    [TestCase((byte) 0x78, 960)]  // Hybrid 20 ms (config 15)
    [TestCase((byte) 0xF8, 960)]  // CELT 20 ms (config 31)
    [TestCase((byte) 0xF9, 1920)] // CELT 20 ms, 2 frames
    [TestCase((byte) 0x18, 2880)] // SILK 60 ms
    public void PacketDuration(byte toc, int samples)
    {
        Assert.That(OpusPacket.GetSampleCount(new[] { toc, (byte) 0 }, Rate), Is.EqualTo(samples));
    }
}
