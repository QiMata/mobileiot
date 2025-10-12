using Plugin.Maui.Audio;
using QiMata.MobileIoT.Services.Interfaces;

namespace QiMata.MobileIoT.Services;

public class AudioModemService : IAudioModemService
{
    private readonly IAudioRecorder _recorder;
    private readonly IAudioDecoder _decoder;
    private CancellationTokenSource? _cts;
    private readonly Func<Task> _requestMicrophonePermission;

    public event EventHandler<string>? DataReceived;

    public AudioModemService(
        IAudioManager audioManager,
        IAudioDecoder decoder,
        Func<Task>? requestMicrophonePermission = null,
        IAudioRecorder? recorder = null)
    {
        _recorder = recorder ?? audioManager.CreateAudioRecorder();
        _decoder = decoder;
        _requestMicrophonePermission = requestMicrophonePermission ?? (() => Permissions.RequestAsync<Permissions.Microphone>());
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        if (_cts != null)
            return;

        await _requestMicrophonePermission().ConfigureAwait(false);
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        await _recorder.StartAsync();
        _ = Task.Run(() => DecodeLoopAsync(_cts.Token));
    }

    public async Task StopAsync()
    {
        if (_cts == null)
            return;
        _cts.Cancel();
        await _recorder.StopAsync();
        _cts.Dispose();
        _cts = null;
    }

    private async Task DecodeLoopAsync(CancellationToken token)
    {
        using var stream = _recorder.GetAudioStream();
        var buffer = new byte[4096];
        while (!token.IsCancellationRequested)
        {
            int read = await stream.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);
            if (read <= 0)
            {
                continue;
            }

            var result = await _decoder.TryDecodeAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(result))
            {
                DataReceived?.Invoke(this, result);
            }
        }
    }
}
