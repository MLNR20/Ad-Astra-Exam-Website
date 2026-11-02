using System.Text.RegularExpressions;

namespace AAExamManagementSystem.Tests;

/// <summary>
/// Stored times are UTC and the host runs on UTC, so every date shown to a person has to go through LocalTime
/// (Philippine Standard Time). These checks fail when a page formats a stored date directly, or reads the host's clock.
/// </summary>
public class LocalTimeUsageTests
{
    private static readonly string[] SourceFolders = { "AAExamManagementSystem", "AAExamManagementSystem.Models" };

    private static readonly string[] AllowedFiles = { "LocalTime.cs", "ExamClock.cs" };

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !directory.GetFiles("*.slnx").Any())
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not find the solution folder.");
    }

    private static IEnumerable<(string File, int Line, string Text)> SourceLines()
    {
        var root = RepositoryRoot();
        foreach (var folder in SourceFolders)
        {
            var files = Directory.EnumerateFiles(Path.Combine(root, folder), "*.*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".cs") || f.EndsWith(".cshtml"))
                .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                    && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                .Where(f => !AllowedFiles.Contains(Path.GetFileName(f)));

            foreach (var file in files)
            {
                var lines = File.ReadAllLines(file);
                for (var i = 0; i < lines.Length; i++)
                {
                    yield return (Path.GetRelativePath(root, file), i + 1, lines[i]);
                }
            }
        }
    }

    private static void AssertNone(Regex pattern, string because)
    {
        var offenders = SourceLines()
            .Where(l => pattern.IsMatch(l.Text))
            .Select(l => $"{l.File}:{l.Line}  {l.Text.Trim()}")
            .ToList();

        Assert.True(offenders.Count == 0, $"{because}{Environment.NewLine}{string.Join(Environment.NewLine, offenders)}");
    }

    [Fact]
    public void NothingReadsTheHostsLocalClock()
    {
        AssertNone(
            new Regex(@"DateTime\.(Now|Today)\b|DateTimeOffset\.Now\b"),
            "Use ExamClock.Now / LocalTime.Now instead of the host clock:");
    }

    [Fact]
    public void NothingConvertsToTheHostsLocalTimeZone()
    {
        AssertNone(
            new Regex(@"\.ToLocalTime\(\)"),
            "Use LocalTime.Of(...) instead of ToLocalTime(), which is UTC on the host:");
    }

    [Fact]
    public void StoredDatesAreNeverFormattedDirectly()
    {
        AssertNone(
            new Regex(@"(?<!LocalTime\.Of\()\b\w+(\.\w+)*\.(CreatedAt|DateCreated|DateUpdated|DateApplied)\??\.ToString\("),
            "Wrap created/updated/applied dates in LocalTime.Of(...) before formatting:");
    }
}
