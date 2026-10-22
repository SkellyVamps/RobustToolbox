using System;
using System.Collections.Generic;
using System.Linq;
using OpenTK.Audio.OpenAL;
using Robust.Client.Audio.Realtime;
using Robust.Client.Audio.Sources;
using Robust.Shared.Audio.Sources;

namespace Robust.Client.Audio;

internal sealed partial class AudioManager : IRealtimeAudioManager
{
    public bool CaptureSupported => _audioInitialized && ALC.IsExtensionPresent(ALDevice.Null, "ALC_EXT_CAPTURE");

    public IReadOnlyList<string> GetCaptureDevices()
    {
        if (!CaptureSupported)
            return Array.Empty<string>();

        return ALC.GetStringList(GetEnumerationStringList.CaptureDeviceSpecifier).ToList();
    }

    public string? GetDefaultCaptureDevice()
    {
        if (!CaptureSupported)
            return null;

        return ALC.GetString(ALDevice.Null, AlcGetString.CaptureDefaultDeviceSpecifier);
    }

    public IAudioCaptureDevice? OpenCaptureDevice(string? deviceName, int sampleRate, int bufferSamples)
    {
        if (!CaptureSupported)
            return null;

        if (string.IsNullOrEmpty(deviceName))
            deviceName = null;

        var device = ALC.CaptureOpenDevice(deviceName, sampleRate, ALFormat.Mono16, bufferSamples);
        if (device == ALCaptureDevice.Null)
        {
            OpenALSawmill.Warning("Unable to open capture device '{0}': {1}",
                deviceName ?? "<default>", ALC.GetError(ALDevice.Null));
            return null;
        }

        var name = deviceName ?? GetDefaultCaptureDevice() ?? "<default>";
        OpenALSawmill.Debug("Opened capture device '{0}' at {1} Hz", name, sampleRate);
        return new AudioCaptureDevice(device, name, sampleRate, OpenALSawmill);
    }

    public IStreamingAudioSource? CreateStreamingSource(int sampleRate, int maxQueuedChunks = 16)
    {
        if (maxQueuedChunks <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxQueuedChunks));

        var source = AL.GenSource();

        if (!AL.IsSource(source))
        {
            OpenALSawmill.Error("Failed to generate streaming source. Too many simultaneous audio streams?");
            return null;
        }

        var audioSource = new StreamingAudioSource(this, source, AL.GenBuffers(maxQueuedChunks), sampleRate);
        ApplyDefaultParams(audioSource);
        _checkAlError();
        return audioSource;
    }
}
