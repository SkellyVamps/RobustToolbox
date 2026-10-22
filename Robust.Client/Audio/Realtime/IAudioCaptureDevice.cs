using System;
using Robust.Shared.Analyzers;

namespace Robust.Client.Audio.Realtime;

/// <summary>
/// An open audio input (microphone) device. Produces mono signed 16-bit samples.
/// </summary>
/// <remarks>
/// Must be polled regularly with <see cref="ReadSamples"/>; the device has a fixed internal ring buffer and drops
/// samples if it is not drained in time.
/// </remarks>
[NotContentImplementable]
public interface IAudioCaptureDevice : IDisposable
{
    /// <summary>
    /// The name of the device that was actually opened.
    /// </summary>
    string DeviceName { get; }

    int SampleRate { get; }

    /// <summary>
    /// Whether <see cref="Start"/> has been called and the device is capturing.
    /// </summary>
    bool Capturing { get; }

    /// <summary>
    /// False if the backend reports the device was disconnected (e.g. a USB microphone was unplugged).
    /// A disconnected device never recovers; dispose it and open a new one.
    /// </summary>
    bool Connected { get; }

    bool Disposed { get; }

    void Start();

    void Stop();

    /// <summary>
    /// Number of captured samples that can currently be read.
    /// </summary>
    int AvailableSamples { get; }

    /// <summary>
    /// Reads up to <c>buffer.Length</c> captured samples.
    /// </summary>
    /// <returns>Number of samples written into <paramref name="buffer"/>.</returns>
    int ReadSamples(Span<short> buffer);
}
