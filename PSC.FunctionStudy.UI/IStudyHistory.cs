namespace PSC.FunctionStudy.UI;

/// <summary>
/// The last few functions this person studied. The UI does not care where they are kept —
/// the browser's localStorage today, a file in a native shell, a per-user table once there is
/// a backend. Keeping the interface here and the implementation in the host is the same split
/// as the render mode: nothing web-specific leaks into the shared components.
/// </summary>
public interface IStudyHistory
{
    ValueTask<IReadOnlyList<string>> RecentAsync(CancellationToken cancellationToken = default);

    ValueTask RecordAsync(string input, CancellationToken cancellationToken = default);
}

/// <summary>
/// Remembers nothing. Used while prerendering on the server, where there is no browser storage
/// to read, and by any host that does not want history at all.
/// </summary>
public sealed class NullStudyHistory : IStudyHistory
{
    public ValueTask<IReadOnlyList<string>> RecentAsync(CancellationToken cancellationToken = default) =>
        ValueTask.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

    public ValueTask RecordAsync(string input, CancellationToken cancellationToken = default) =>
        ValueTask.CompletedTask;
}
