using QiMata.MobileIoT.Shared.Services;
using Plugin.Maui.Audio;
using QiMata.MobileIoT.Shared.Services.Interfaces;

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
        _recorder = recorder ?? audioManager.CreateRecorder();
        _decoder = decoder;
        _requestMicrophonePermission = requestMicrophonePermission ?? RequestMicrophonePermissionAsync;
    }

    private static Task RequestMicrophonePermissionAsync()
    {
#if ANDROID || IOS || MACCATALYST || WINDOWS
        return Permissions.RequestAsync<Permissions.Microphone>();
#else
        return Task.CompletedTask;
#endif
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        if (_cts != null)
            return;

        await _requestMicrophonePermission().ConfigureAwait(false);
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        await _recorder.StartAsync();
    }

    public async Task StopAsync()
    {
        if (_cts == null)
            return;
        _cts.Cancel();
        var source = await _recorder.StopAsync();
        await DecodeAsync(source.GetAudioStream(), CancellationToken.None).ConfigureAwait(false);
        _cts.Dispose();
        _cts = null;
    }

    private async Task DecodeAsync(Stream stream, CancellationToken token)
    {
        using (stream)
        {
            var buffer = new byte[4096];
            while (!token.IsCancellationRequested)
            {
                int read = await stream.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);
                if (read <= 0)
                    break;

                var result = await _decoder.TryDecodeAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(result))
                {
                    DataReceived?.Invoke(this, result);
                }
            }
        }
    }
}
