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
        recorder.Setup(r => r.GetAudioStream()).Returns(new FakeAudioStream());
        audioManager.Setup(a => a.CreateAudioRecorder()).Returns(recorder.Object);

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
        var stream = new FakeAudioStream();
        recorder.Setup(r => r.StartAsync()).Returns(Task.CompletedTask);
        recorder.Setup(r => r.StopAsync()).Returns(Task.CompletedTask);
        recorder.Setup(r => r.GetAudioStream()).Returns(stream);
        audioManager.Setup(a => a.CreateAudioRecorder()).Returns(recorder.Object);

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
        stream.Enqueue(new byte[] { 0x01, 0x00, 0x02, 0x00 });

        await Task.WhenAny(tcs.Task, Task.Delay(1000));

        Assert.Equal(1, permissionRequested);
        Assert.Equal("decoded", message);

        await service.StopAsync();
        await service.StopAsync();
        recorder.Verify(r => r.StopAsync(), Times.Once);
    }

    private sealed class FakeAudioStream : Stream
    {
        private readonly Channel<byte[]> _channel = Channel.CreateUnbounded<byte[]>();

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public void Enqueue(byte[] data)
        {
            _channel.Writer.TryWrite(data);
        }

        public override void Flush()
        {
        }

        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            try
            {
                var data = await _channel.Reader.ReadAsync(cancellationToken);
                var span = new Span<byte>(buffer, offset, count);
                data.CopyTo(span);
                return data.Length;
            }
            catch (OperationCanceledException)
            {
                return 0;
            }
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
