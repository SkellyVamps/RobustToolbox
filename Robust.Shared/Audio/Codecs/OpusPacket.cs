using System;

namespace Robust.Shared.Audio.Codecs;

/// <summary>
/// Cheap structural inspection of Opus packets (RFC 6716 section 3) without decoding them.
/// Lets a server sanity-check relayed packets without running a decoder.
/// </summary>
public static class OpusPacket
{
    /// <summary>
    /// Longest duration a single Opus packet may encode, in milliseconds.
    /// </summary>
    public const int MaxPacketDurationMs = 120;

    /// <summary>
    /// Returns the number of samples per channel the packet decodes to at <paramref name="sampleRate"/>,
    /// or -1 if the packet is structurally invalid.
    /// </summary>
    public static int GetSampleCount(ReadOnlySpan<byte> packet, int sampleRate)
    {
        if (packet.Length < 1 || sampleRate <= 0)
            return -1;

        var toc = packet[0];
        int frames;

        switch (toc & 0x3)
        {
            case 0:
                frames = 1;
                break;
            case 1:
            case 2:
                frames = 2;
                break;
            default:
                if (packet.Length < 2)
                    return -1;

                frames = packet[1] & 0x3F;
                if (frames == 0)
                    return -1;
                break;
        }

        // Frame duration in units of 1/400 s (2.5 ms).
        var frameUnits = GetFrameDurationUnits(toc);
        var totalUnits = frames * frameUnits;

        if (totalUnits * 25 > MaxPacketDurationMs * 10)
            return -1;

        return totalUnits * sampleRate / 400;
    }

    /// <summary>
    /// Frame duration encoded in the TOC byte, in units of 2.5 ms.
    /// </summary>
    private static int GetFrameDurationUnits(byte toc)
    {
        var config = toc >> 3;

        if (config < 12)
        {
            // SILK-only: 10, 20, 40, 60 ms
            return (config & 0x3) switch
            {
                0 => 4,
                1 => 8,
                2 => 16,
                _ => 24,
            };
        }

        if (config < 16)
        {
            // Hybrid: 10, 20 ms
            return (config & 0x1) == 0 ? 4 : 8;
        }

        // CELT-only: 2.5, 5, 10, 20 ms
        return 1 << (config & 0x3);
    }
}
