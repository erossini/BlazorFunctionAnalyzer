using Microsoft.JSInterop;
using PSC.FunctionStudy.UI;

namespace PSC.FunctionStudy.Web.Client;

/// <summary>
/// Keeps the recent list in the browser's localStorage. Per device, never sent anywhere,
/// no account needed. Entries are stored one per line — the parser rejects newlines, so an
/// expression can never contain the separator.
/// </summary>
public sealed class LocalStorageStudyHistory(IJSRuntime js) : IStudyHistory
{
    const string Key = "psc.functionstudy.recent";
    const int Limit = 5;
    const int MaxLength = 200;

    public async ValueTask<IReadOnlyList<string>> RecentAsync(CancellationToken cancellationToken = default) =>
        Split(await ReadAsync(cancellationToken));

    public async ValueTask RecordAsync(string input, CancellationToken cancellationToken = default)
    {
        input = input.Trim();
        if (input.Length is 0 or > MaxLength) return;

        var recent = Split(await ReadAsync(cancellationToken)).ToList();
        recent.RemoveAll(entry => string.Equals(entry, input, StringComparison.Ordinal));
        recent.Insert(0, input);
        if (recent.Count > Limit) recent.RemoveRange(Limit, recent.Count - Limit);

        try
        {
            await js.InvokeVoidAsync("localStorage.setItem", cancellationToken, Key, string.Join('\n', recent));
        }
        catch (JSException)
        {
            // storage disabled or full — the study itself still worked, so say nothing
        }
    }

    async ValueTask<string?> ReadAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await js.InvokeAsync<string?>("localStorage.getItem", cancellationToken, Key);
        }
        catch (JSException)
        {
            return null;
        }
    }

    static IReadOnlyList<string> Split(string? stored) =>
        string.IsNullOrEmpty(stored)
            ? Array.Empty<string>()
            : stored.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Take(Limit)
                    .ToArray();
}
