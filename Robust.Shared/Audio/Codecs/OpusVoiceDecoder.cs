using System;
using Concentus;

namespace Robust.Shared.Audio.Codecs;

/// <summary>
/// Mono Opus decoder with packet loss concealment. Not thread safe.
/// </summary>
public sealed class OpusVoiceDecoder
{
    private readonly IOpusDecoder _decoder;

    public int SampleRate { get; }

    /// <param name="sampleRate">One of 8000, 12000, 16000, 24000 or 48000.</param>
    public OpusVoiceDecoder(int sampleRate)
    {
        SampleRate = sampleRate;
        _decoder = OpusCodecFactory.CreateDecoder(sampleRate, 1);
    }

    /// <summary>
    /// Decodes a packet into <paramref name="output"/>.
    /// </summary>
    /// <param name="packet">The packet. Malformed packets are rejected rather than throwing.</param>
    /// <param name="output">Destination. Its length is the maximum frame size accepted.</param>
    /// <param name="decodeFec">
    /// Decode the forward error correction data embedded in <paramref name="packet"/>, which reconstructs the frame
    /// <i>before</i> it. Use when that previous packet was lost; <paramref name="output"/> must then be exactly one
    /// frame long.
    /// </param>
    /// <returns>Samples decoded, or -1 if the packet was invalid.</returns>
    public int Decode(ReadOnlySpan<byte> packet, Span<short> output, bool decodeFec = false)
    {
        if (packet.Length == 0)
            return -1;

        if (OpusPacket.GetSampleCount(packet, SampleRate) is var count && (count < 0 || count > output.Length))
            return -1;

        try
        {
            return _decoder.Decode(packet, output, output.Length, decodeFec);
        }
        catch (Exception)
        {
            return -1;
        }
    }

    /// <summary>
    /// Synthesizes a concealment frame for a packet that never arrived.
    /// </summary>
    /// <param name="output">Destination, must be exactly the length of the missing frame.</param>
    /// <returns>Samples generated, or -1 on failure.</returns>
    public int DecodeLost(Span<short> output)
    {
        try
        {
            return _decoder.Decode(ReadOnlySpan<byte>.Empty, output, output.Length, false);
        }
        catch (Exception)
        {
            return -1;
        }
    }

    public void Reset()
    {
        _decoder.ResetState();
    }
}
