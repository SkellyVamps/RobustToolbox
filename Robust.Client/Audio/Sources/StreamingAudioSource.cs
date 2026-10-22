using System;
using System.Collections.Generic;
using OpenTK.Audio.OpenAL;
using Robust.Shared.Audio.Sources;

namespace Robust.Client.Audio.Sources;

/// <summary>
/// Plays mono 16-bit PCM chunks pushed at runtime through a pool of OpenAL buffers.
/// </summary>
internal sealed class StreamingAudioSource : BaseAudioSource, IStreamingAudioSource
{
    private readonly int[] _bufferHandles;
    private readonly Stack<int> _freeBuffers = new();
    private readonly Dictionary<int, int> _bufferSampleCounts = new();

    // Sum of samples in all currently queued buffers.
    private int _queuedTotal;

    // Set when content explicitly stops the source, so queueing doesn't immediately restart it.
    private bool _stopped;

    public int SampleRate { get; }

    public int MaxQueuedChunks => _bufferHandles.Length;

    public StreamingAudioSource(AudioManager master, int sourceHandle, int[] bufferHandles, int sampleRate)
        : base(master, sourceHandle)
    {
        _bufferHandles = bufferHandles;
        SampleRate = sampleRate;

        foreach (var handle in bufferHandles)
        {
            _freeBuffers.Push(handle);
        }
    }

    public override bool Playing
    {
        get
        {
            _checkDisposed();
            var state = AL.GetSource(SourceHandle, ALGetSourcei.SourceState);
            Master._checkAlError();
            return state == (int) ALSourceState.Playing;
        }
        set
        {
            if (value)
            {
                _checkDisposed();
                _stopped = false;

                if (_queuedTotal > 0)
                    AL.SourcePlay(SourceHandle);
            }
            else
            {
                if (_isDisposed())
                    return;

                _stopped = true;
                AL.SourceStop(SourceHandle);
            }

            Master._checkAlError();
        }
    }

    public int QueuedSamples
    {
        get
        {
            _checkDisposed();
            ReclaimProcessed();

            if (_queuedTotal == 0)
                return 0;

            // Offset into the buffer currently playing.
            AL.GetSource(SourceHandle, ALGetSourcei.SampleOffset, out var offset);
            Master._checkAlError();
            return Math.Max(0, _queuedTotal - offset);
        }
    }

    public bool QueueSamples(ReadOnlySpan<short> samples)
    {
        _checkDisposed();

        if (samples.Length == 0)
            return true;

        ReclaimProcessed();

        if (!_freeBuffers.TryPop(out var buffer))
            return false;

        unsafe
        {
            fixed (short* ptr = samples)
            {
                AL.BufferData(buffer, ALFormat.Mono16, (IntPtr) ptr, samples.Length * sizeof(short), SampleRate);
            }

            AL.SourceQueueBuffers(SourceHandle, 1, &buffer);
        }

        Master._checkAlError();

        _bufferSampleCounts[buffer] = samples.Length;
        _queuedTotal += samples.Length;

        // Restart after an underrun (OpenAL stops the source once it runs out of queued buffers).
        if (!_stopped)
        {
            var state = AL.GetSource(SourceHandle, ALGetSourcei.SourceState);
            if (state != (int) ALSourceState.Playing)
                AL.SourcePlay(SourceHandle);

            Master._checkAlError();
        }

        return true;
    }

    public void ClearQueue()
    {
        _checkDisposed();

        // Stopping marks every queued buffer as processed so they can all be unqueued.
        AL.SourceStop(SourceHandle);
        ReclaimProcessed();
        Master._checkAlError();
    }

    private unsafe void ReclaimProcessed()
    {
        AL.GetSource(SourceHandle, ALGetSourcei.BuffersProcessed, out var processed);
        if (processed <= 0)
            return;

        Span<int> handles = stackalloc int[Math.Min(processed, _bufferHandles.Length)];
        fixed (int* ptr = handles)
        {
            AL.SourceUnqueueBuffers(SourceHandle, handles.Length, ptr);
        }

        foreach (var handle in handles)
        {
            if (_bufferSampleCounts.Remove(handle, out var count))
                _queuedTotal -= count;

            _freeBuffers.Push(handle);
        }
    }

    ~StreamingAudioSource()
    {
        Dispose(false);
    }

    protected override void Dispose(bool disposing)
    {
        if (SourceHandle == -1)
            return;

        if (!Master.IsMainThread())
        {
            // OpenAL calls must happen on the main thread, defer cleanup.
            Master.DeleteBufferedSourceOnMainThread(SourceHandle, FilterHandle);

            foreach (var handle in _bufferHandles)
            {
                Master.DeleteAudioBufferOnMainThread(handle);
            }
        }
        else
        {
            if (FilterHandle != 0)
                ALC.EFX.DeleteFilter(FilterHandle);

            AL.SourceStop(SourceHandle);
            AL.DeleteSource(SourceHandle);
            AL.DeleteBuffers(_bufferHandles);
            Master.RemoveBufferedAudioSource(SourceHandle);
            Master._checkAlError();
        }

        FilterHandle = 0;
        SourceHandle = -1;
    }
}
