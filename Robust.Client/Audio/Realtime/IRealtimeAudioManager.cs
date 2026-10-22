using System.Collections.Generic;
using Robust.Shared.Analyzers;
using Robust.Shared.Audio.Sources;

namespace Robust.Client.Audio.Realtime;

/// <summary>
/// Low-level primitives for real-time audio: microphone capture and streaming playback.
/// Intended for things like voice chat that need to push PCM to and from the audio backend at runtime.
/// </summary>
[NotContentImplementable]
public interface IRealtimeAudioManager
{
    /// <summary>
    /// Whether audio capture is possible at all. False on headless clients or if the backend lacks capture support.
    /// </summary>
    bool CaptureSupported { get; }

    /// <summary>
    /// Names of the available capture (microphone) devices. Can be passed to <see cref="OpenCaptureDevice"/>.
    /// </summary>
    IReadOnlyList<string> GetCaptureDevices();

    string? GetDefaultCaptureDevice();

    /// <summary>
    /// Opens a capture device.
    /// </summary>
    /// <param name="deviceName">Device to open, or null/empty for the system default.</param>
    /// <param name="sampleRate">Requested sample rate.</param>
    /// <param name="bufferSamples">Size of the backend ring buffer in samples.</param>
    /// <returns>Null if the device could not be opened (missing, in use, permission denied...).</returns>
    IAudioCaptureDevice? OpenCaptureDevice(string? deviceName, int sampleRate, int bufferSamples);

    /// <summary>
    /// Creates a source that plays mono 16-bit PCM pushed with <see cref="IStreamingAudioSource.QueueSamples"/>.
    /// </summary>
    /// <param name="sampleRate">Sample rate of the samples that will be queued.</param>
    /// <param name="maxQueuedChunks">How many chunks may be queued at once.</param>
    /// <returns>Null if no more sources could be created.</returns>
    IStreamingAudioSource? CreateStreamingSource(int sampleRate, int maxQueuedChunks = 16);
}
