using Plugin.Maui.Audio;

namespace QiMata.MobileIoT.Services;

public class AudioModemService : IAudioModemService
{
    private readonly IAudioRecorder _recorder;
    private CancellationTokenSource? _cts;

    public event EventHandler<string>? DataReceived;

    public AudioModemService(IAudioManager audioManager)
    {
        _recorder = audioManager.CreateAudioRecorder();
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        if (_cts != null)
            return;

        await Permissions.RequestAsync<Permissions.Microphone>();
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
            int read = await stream.ReadAsync(buffer, 0, buffer.Length, token);
            if (read <= 0) continue;
            // TODO: implement FSK/DTMF decoding of 'buffer'.
            // For demo purposes we just report raw sample count.
            DataReceived?.Invoke(this, $"RX {read} bytes");
        }
    }
}
