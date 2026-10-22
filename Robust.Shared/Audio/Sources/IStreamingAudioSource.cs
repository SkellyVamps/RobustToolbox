using System;
using Robust.Shared.Analyzers;

namespace Robust.Shared.Audio.Sources;

/// <summary>
/// An audio source that plays PCM samples pushed to it at runtime, e.g. decoded voice or synthesized audio.
/// </summary>
/// <remarks>
/// Samples are mono, signed 16-bit, at <see cref="SampleRate"/>.
/// If the source runs out of queued samples it stops (an underrun); queueing more samples restarts it automatically
/// unless <see cref="IAudioSource.StopPlaying"/> was called explicitly.
/// Must only be used from the main thread.
/// </remarks>
[NotContentImplementable]
public interface IStreamingAudioSource : IAudioSource
{
    /// <summary>
    /// Sample rate of the samples queued into this source.
    /// </summary>
    int SampleRate { get; }

    /// <summary>
    /// Approximate number of samples queued and not yet played.
    /// </summary>
    int QueuedSamples { get; }

    /// <summary>
    /// Maximum number of chunks that can be queued at once. <see cref="QueueSamples"/> fails when this is exceeded.
    /// </summary>
    int MaxQueuedChunks { get; }

    /// <summary>
    /// Queues a chunk of mono 16-bit samples for playback.
    /// </summary>
    /// <returns>False if the chunk could not be queued because too many chunks are already queued.</returns>
    bool QueueSamples(ReadOnlySpan<short> samples);

    /// <summary>
    /// Drops every queued sample and stops playback.
    /// </summary>
    void ClearQueue();
}
