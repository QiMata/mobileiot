using FluentAssertions;
using Moq;
using QiMata.MobileIoT.Shared.Services.Interfaces;
using QiMata.MobileIoT.ViewModels;
using QiMata.MobileIoT.Shared.Thread.Models;
using QiMata.MobileIoT.Shared.Thread.Services;
using Xunit;

namespace QiMata.MobileIoT.Shared.Thread.Tests;

public class ThreadViewModelTests
{
    private readonly Mock<IThreadDemoService> _serviceMock = new();
    private readonly Mock<INavigationService> _navMock = new();
    private readonly Mock<IAppLogger> _loggerMock = new();

    private ThreadViewModel CreateVm() => new(_serviceMock.Object, _navMock.Object, _loggerMock.Object);

    [Fact]
    public void InitialState_HasDefaults()
    {
        var vm = CreateVm();

        vm.Role.Should().Be("--");
        vm.DatasetHex.Should().Be("--");
        vm.IsBusy.Should().BeFalse();
        vm.BridgeUrl.Should().Be("http://raspberrypi.local:8080");
        vm.Payload.Should().Be("ping");
        vm.LogEntries.Should().BeEmpty();
    }

    [Fact]
    public async Task RefreshStatus_UpdatesProperties()
    {
        _serviceMock
            .Setup(s => s.GetStatusAsync(It.IsAny<ThreadConnectionSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ThreadStatusSnapshot
            {
                Role = "leader",
                DatasetHex = "abc123",
                MeshLocalAddresses = ["fd00::1", "fd00::2"],
                Rloc16 = "0x0400",
                SourceMode = "mock",
                LastUpdatedUtc = DateTime.UtcNow
            });

        var vm = CreateVm();
        await vm.RefreshStatusCommand.ExecuteAsync(null);

        vm.Role.Should().Be("leader");
        vm.DatasetHex.Should().Be("abc123");
        vm.MeshAddresses.Should().Contain("fd00::1");
        vm.Rloc16.Should().Be("0x0400");
        vm.SourceMode.Should().Be("mock");
        vm.IsBusy.Should().BeFalse();
        vm.LogEntries.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task SendPing_Success_LogsResult()
    {
        _serviceMock
            .Setup(s => s.SendPingAsync(It.IsAny<ThreadConnectionSettings>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ThreadPingResult
            {
                Success = true,
                RequestPayload = "ping",
                ResponsePayload = "ECHO: ping",
                RoundTripMs = 10.5
            });

        var vm = CreateVm();
        await vm.SendPingCommand.ExecuteAsync(null);

        vm.IsBusy.Should().BeFalse();
        vm.LogText.Should().Contain("ECHO: ping");
        vm.LogText.Should().Contain("RTT=10.5ms");
    }

    [Fact]
    public async Task SendPing_Failure_LogsError()
    {
        _serviceMock
            .Setup(s => s.SendPingAsync(It.IsAny<ThreadConnectionSettings>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ThreadPingResult
            {
                Success = false,
                RequestPayload = "ping",
                Error = "CoAP request timed out"
            });

        var vm = CreateVm();
        await vm.SendPingCommand.ExecuteAsync(null);

        vm.LogText.Should().Contain("CoAP request timed out");
    }

    [Fact]
    public async Task RefreshStatus_Exception_LogsError()
    {
        _serviceMock
            .Setup(s => s.GetStatusAsync(It.IsAny<ThreadConnectionSettings>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Bridge unreachable: Connection refused"));

        var vm = CreateVm();
        await vm.RefreshStatusCommand.ExecuteAsync(null);

        vm.IsBusy.Should().BeFalse();
        vm.LogText.Should().Contain("Bridge unreachable");
    }

    [Fact]
    public void ClearLog_RemovesAllEntries()
    {
        var vm = CreateVm();
        // Manually add entries
        vm.LogEntries.Add(new ThreadLogEntry { TimestampUtc = DateTime.UtcNow, Message = "test" });
        vm.ClearLogCommand.Execute(null);

        vm.LogEntries.Should().BeEmpty();
        vm.LogText.Should().BeEmpty();
    }

    [Fact]
    public async Task LogCapAt200Entries()
    {
        // Set up mock to return quickly
        _serviceMock
            .Setup(s => s.GetStatusAsync(It.IsAny<ThreadConnectionSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ThreadStatusSnapshot
            {
                Role = "leader",
                DatasetHex = "x",
                MeshLocalAddresses = [],
                Rloc16 = "0x0",
                SourceMode = "mock",
                LastUpdatedUtc = DateTime.UtcNow
            });

        var vm = CreateVm();

        // Fill log beyond capacity
        for (int i = 0; i < 210; i++)
        {
            await vm.RefreshStatusCommand.ExecuteAsync(null);
        }

        vm.LogEntries.Count.Should().BeLessThanOrEqualTo(ThreadViewModel.MaxLogEntries);
    }

    [Fact]
    public async Task BusyState_PreventsCommand()
    {
        var tcs = new TaskCompletionSource<ThreadStatusSnapshot>();
        _serviceMock
            .Setup(s => s.GetStatusAsync(It.IsAny<ThreadConnectionSettings>(), It.IsAny<CancellationToken>()))
            .Returns(tcs.Task);

        var vm = CreateVm();
        var task = vm.RefreshStatusCommand.ExecuteAsync(null);

        // While busy, CanExecute should be false
        vm.IsBusy.Should().BeTrue();
        vm.RefreshStatusCommand.CanExecute(null).Should().BeFalse();
        vm.SendPingCommand.CanExecute(null).Should().BeFalse();

        // Complete the task
        tcs.SetResult(new ThreadStatusSnapshot
        {
            Role = "leader", DatasetHex = "", MeshLocalAddresses = [],
            Rloc16 = "", SourceMode = "mock", LastUpdatedUtc = DateTime.UtcNow
        });
        await task;

        vm.IsBusy.Should().BeFalse();
        vm.RefreshStatusCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public async Task NavigateBack_CallsNavigationService()
    {
        _navMock.Setup(n => n.GoBackAsync()).Returns(Task.CompletedTask);

        var vm = CreateVm();
        await vm.NavigateBackCommand.ExecuteAsync(null);

        _navMock.Verify(n => n.GoBackAsync(), Times.Once);
    }
}
