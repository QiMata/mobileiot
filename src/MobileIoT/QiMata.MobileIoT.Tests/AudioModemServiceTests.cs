using Moq;
using Plugin.Maui.Audio;
using QiMata.MobileIoT.Services;
using QiMata.MobileIoT.Services.Interfaces;
using System;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Xunit;

namespace QiMata.MobileIoT.Tests;

public class AudioModemServiceTests
{
    [Fact]
    public async Task StartAsync_RequestsPermissionAndStartsRecorderOnce()
    {
        var permissionRequested = 0;
        var audioManager = new Mock<IAudioManager>();
        var recorder = new Mock<IAudioRecorder>();
        recorder.Setup(r => r.StartAsync()).Returns(Task.CompletedTask).Verifiable();
        audioManager.Setup(a => a.CreateRecorder()).Returns(recorder.Object);

        var decoder = new Mock<IAudioDecoder>();
        decoder.Setup(d => d.TryDecodeAsync(It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()))
               .Returns(ValueTask.FromResult<string?>(null));

        var service = new AudioModemService(audioManager.Object, decoder.Object, () =>
        {
            permissionRequested++;
            return Task.CompletedTask;
        }, recorder.Object);

        await service.StartAsync();
        await service.StartAsync();

        Assert.Equal(1, permissionRequested);
        recorder.Verify(r => r.StartAsync(), Times.Once);
    }

    [Fact]
    public async Task DecodeLoop_RaisesDataReceivedWhenDecoderEmits()
    {
        var permissionRequested = 0;
        var audioManager = new Mock<IAudioManager>();
        var recorder = new Mock<IAudioRecorder>();
        var stream = new MemoryStream(new byte[] { 0x01, 0x00, 0x02, 0x00 });
        var source = new Mock<IAudioSource>();
        source.Setup(s => s.GetAudioStream()).Returns(stream);
        recorder.Setup(r => r.StartAsync()).Returns(Task.CompletedTask);
        recorder.Setup(r => r.StopAsync()).ReturnsAsync(source.Object);
        audioManager.Setup(a => a.CreateRecorder()).Returns(recorder.Object);

        var decoder = new Mock<IAudioDecoder>();
        decoder.Setup(d => d.TryDecodeAsync(It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()))
               .Returns<ReadOnlyMemory<byte>, CancellationToken>((buffer, _) =>
                   ValueTask.FromResult<string?>(buffer.Length > 0 ? "decoded" : null));

        var service = new AudioModemService(audioManager.Object, decoder.Object, () =>
        {
            permissionRequested++;
            return Task.CompletedTask;
        }, recorder.Object);

        string? message = null;
        var tcs = new TaskCompletionSource<string?>();
        service.DataReceived += (_, payload) =>
        {
            message = payload;
            tcs.TrySetResult(payload);
        };

        await service.StartAsync();
        await service.StopAsync();
        await Task.WhenAny(tcs.Task, Task.Delay(1000));

        Assert.Equal(1, permissionRequested);
        Assert.Equal("decoded", message);
        await service.StopAsync();
        recorder.Verify(r => r.StopAsync(), Times.Once);
    }
}
