using System;
using Concentus;
using Concentus.Enums;

namespace Robust.Shared.Audio.Codecs;

/// <summary>
/// Mono Opus encoder tuned for voice. Not thread safe.
/// </summary>
public sealed class OpusVoiceEncoder
{
    private readonly IOpusEncoder _encoder;

    public int SampleRate { get; }

    /// <param name="sampleRate">One of 8000, 12000, 16000, 24000 or 48000.</param>
    public OpusVoiceEncoder(int sampleRate)
    {
        SampleRate = sampleRate;
        _encoder = OpusCodecFactory.CreateEncoder(sampleRate, 1, OpusApplication.OPUS_APPLICATION_VOIP);
        _encoder.SignalType = OpusSignal.OPUS_SIGNAL_VOICE;
        _encoder.UseVBR = true;
    }

    /// <summary>
    /// Target bitrate in bits per second.
    /// </summary>
    public int Bitrate
    {
        get => _encoder.Bitrate;
        set => _encoder.Bitrate = Math.Clamp(value, 6000, 128000);
    }

    /// <summary>
    /// Encoder complexity, 0 (fastest) to 10 (best quality).
    /// </summary>
    public int Complexity
    {
        get => _encoder.Complexity;
        set => _encoder.Complexity = Math.Clamp(value, 0, 10);
    }

    /// <summary>
    /// Embeds redundant data for the previous frame so receivers can recover single lost packets.
    /// </summary>
    public bool UseInbandFec
    {
        get => _encoder.UseInbandFEC;
        set => _encoder.UseInbandFEC = value;
    }

    /// <summary>
    /// Expected packet loss in percent; higher values make FEC more aggressive.
    /// </summary>
    public int ExpectedPacketLossPercent
    {
        get => _encoder.PacketLossPercent;
        set => _encoder.PacketLossPercent = Math.Clamp(value, 0, 100);
    }

    /// <summary>
    /// Discontinuous transmission: emit tiny packets during silence.
    /// </summary>
    public bool UseDtx
    {
        get => _encoder.UseDTX;
        set => _encoder.UseDTX = value;
    }

    /// <summary>
    /// Encodes one frame. <paramref name="pcm"/> must be exactly 2.5, 5, 10, 20, 40 or 60 ms long.
    /// </summary>
    /// <returns>Number of bytes written to <paramref name="output"/>, or -1 on failure.</returns>
    public int Encode(ReadOnlySpan<short> pcm, Span<byte> output)
    {
        try
        {
            return _encoder.Encode(pcm, pcm.Length, output, output.Length);
        }
        catch (Exception)
        {
            return -1;
        }
    }

    public void Reset()
    {
        _encoder.ResetState();
    }
}
