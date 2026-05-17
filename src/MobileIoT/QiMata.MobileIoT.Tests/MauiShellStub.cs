namespace Microsoft.Maui.Controls;

public sealed class Shell
{
    public static Shell Current { get; } = new();

    public Task GoToAsync(string route) => Task.CompletedTask;
}
