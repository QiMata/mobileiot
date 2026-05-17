using FluentAssertions;
using Moq;
using QiMata.MobileIoT.Shared.Thread.Models;
using QiMata.MobileIoT.Shared.Thread.Services;
using Xunit;

namespace QiMata.MobileIoT.Shared.Thread.Tests;

public class ThreadDemoServiceTests
{
    private readonly Mock<IThreadBridgeClient> _bridgeMock = new();

    private ThreadDemoService CreateService() => new(_bridgeMock.Object);

    // ---------------------------------------------------------------
    // Mock mode
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetStatusAsync_MockMode_ReturnsDeterministicStatus()
    {
        var svc = CreateService();
        var settings = new ThreadConnectionSettings { UseLiveBridge = false };

        var status = await svc.GetStatusAsync(settings);

        status.Role.Should().Be("leader");
        status.SourceMode.Should().Be("mock");
        status.MeshLocalAddresses.Should().HaveCountGreaterThan(0);
        status.DatasetHex.Should().NotBeNullOrEmpty();
        status.Rloc16.Should().NotBeNullOrEmpty();
        status.LastUpdatedUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task SendPingAsync_MockMode_ReturnsEcho()
    {
        var svc = CreateService();
        var settings = new ThreadConnectionSettings { UseLiveBridge = false };

        var result = await svc.SendPingAsync(settings, "hello");

        result.Success.Should().BeTrue();
        result.RequestPayload.Should().Be("hello");
        result.ResponsePayload.Should().Be("ECHO: hello");
        result.RoundTripMs.Should().BeGreaterThan(0);
        result.Error.Should().BeNull();
    }

    // ---------------------------------------------------------------
    // Live mode – success
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetStatusAsync_LiveMode_DelegatesToBridgeClient()
    {
        var expected = new ThreadStatusSnapshot
        {
            Role = "router",
            DatasetHex = "abcd1234",
            MeshLocalAddresses = ["fd00::1"],
            Rloc16 = "0x1800",
            LastUpdatedUtc = DateTime.UtcNow,
            SourceMode = "live"
        };

        _bridgeMock
            .Setup(b => b.GetStatusAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var svc = CreateService();
        var settings = new ThreadConnectionSettings
        {
            UseLiveBridge = true,
            BridgeBaseUrl = "http://localhost:8080"
        };

        var status = await svc.GetStatusAsync(settings);

        status.Role.Should().Be("router");
        status.SourceMode.Should().Be("live");
        _bridgeMock.Verify(b => b.GetStatusAsync("http://localhost:8080", 3000, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendPingAsync_LiveMode_DelegatesToBridgeClient()
    {
        var expected = new ThreadPingResult
        {
            Success = true,
            RequestPayload = "test",
            ResponsePayload = "ECHO: test",
            RoundTripMs = 12.5
        };

        _bridgeMock
            .Setup(b => b.SendPingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var svc = CreateService();
        var settings = new ThreadConnectionSettings
        {
            UseLiveBridge = true,
            BridgeBaseUrl = "http://localhost:8080",
            TargetNode = "fd00::1"
        };

        var result = await svc.SendPingAsync(settings, "test");

        result.Success.Should().BeTrue();
        result.ResponsePayload.Should().Be("ECHO: test");
    }

    // ---------------------------------------------------------------
    // Live mode – error handling
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetStatusAsync_LiveMode_HttpError_ThrowsWithMessage()
    {
        _bridgeMock
            .Setup(b => b.GetStatusAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var svc = CreateService();
        var settings = new ThreadConnectionSettings
        {
            UseLiveBridge = true,
            BridgeBaseUrl = "http://localhost:8080"
        };

        var act = () => svc.GetStatusAsync(settings);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Bridge unreachable*");
    }

    [Fact]
    public async Task SendPingAsync_LiveMode_HttpError_ReturnsFailureResult()
    {
        _bridgeMock
            .Setup(b => b.SendPingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var svc = CreateService();
        var settings = new ThreadConnectionSettings
        {
            UseLiveBridge = true,
            BridgeBaseUrl = "http://localhost:8080",
            TargetNode = "fd00::1"
        };

        var result = await svc.SendPingAsync(settings, "test");

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Bridge unreachable");
    }

    [Fact]
    public async Task SendPingAsync_LiveMode_Timeout_ReturnsFailureResult()
    {
        _bridgeMock
            .Setup(b => b.SendPingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var svc = CreateService();
        var settings = new ThreadConnectionSettings
        {
            UseLiveBridge = true,
            BridgeBaseUrl = "http://localhost:8080",
            TargetNode = "fd00::1"
        };

        var result = await svc.SendPingAsync(settings, "test");

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("timed out");
    }
}
