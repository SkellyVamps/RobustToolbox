using System;
using OpenTK.Audio.OpenAL;
using Robust.Shared.Log;

namespace Robust.Client.Audio.Realtime;

/// <summary>
/// OpenAL (ALC) capture device wrapper.
/// </summary>
internal sealed class AudioCaptureDevice : IAudioCaptureDevice
{
    // ALC_CONNECTED from ALC_EXT_disconnect, not exposed by OpenTK's enum.
    private const AlcGetInteger AlcConnected = (AlcGetInteger) 0x313;

    private readonly ISawmill _sawmill;
    private readonly bool _supportsDisconnect;
    private ALCaptureDevice _device;

    public string DeviceName { get; }
    public int SampleRate { get; }
    public bool Capturing { get; private set; }
    public bool Disposed => _device == ALCaptureDevice.Null;

    public AudioCaptureDevice(ALCaptureDevice device, string deviceName, int sampleRate, ISawmill sawmill)
    {
        _device = device;
        DeviceName = deviceName;
        SampleRate = sampleRate;
        _sawmill = sawmill;
        _supportsDisconnect = ALC.IsExtensionPresent(new ALDevice(device.Handle), "ALC_EXT_disconnect");
    }

    public bool Connected
    {
        get
        {
            if (Disposed)
                return false;

            if (!_supportsDisconnect)
                return true;

            return ALC.GetInteger(_device, AlcConnected) != 0;
        }
    }

    public void Start()
    {
        if (Disposed || Capturing)
            return;

        ALC.CaptureStart(_device);
        Capturing = true;
    }

    public void Stop()
    {
        if (Disposed || !Capturing)
            return;

        ALC.CaptureStop(_device);
        Capturing = false;
    }

    public int AvailableSamples
    {
        get
        {
            if (Disposed)
                return 0;

            return Math.Max(0, ALC.GetInteger(_device, AlcGetInteger.CaptureSamples));
        }
    }

    public unsafe int ReadSamples(Span<short> buffer)
    {
        if (Disposed)
            return 0;

        var count = Math.Min(buffer.Length, AvailableSamples);
        if (count <= 0)
            return 0;

        fixed (short* ptr = buffer)
        {
            ALC.CaptureSamples(_device, ptr, count);
        }

        var err = ALC.GetError(new ALDevice(_device.Handle));
        if (err != AlcError.NoError)
        {
            _sawmill.Warning($"Error reading capture samples from '{DeviceName}': {err}");
            return 0;
        }

        return count;
    }

    public void Dispose()
    {
        if (Disposed)
            return;

        if (Capturing)
            ALC.CaptureStop(_device);

        ALC.CaptureCloseDevice(_device);
        _device = ALCaptureDevice.Null;
        Capturing = false;
    }
}
