using WinUiAutomation.Runtime;
using WindowsAutomation = UiAutomation.Windows.WinUiAutomation;

namespace WinUiAutomation.SkillRunner;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (!TryParseArguments(args, out string? skillPath, out bool dryRun))
        {
            PrintUsage();
            return 2;
        }

        if (skillPath is null)
        {
            PrintUsage();
            return 0;
        }

        SkillRunResult result;
        try
        {
            using var automation = new WindowsAutomation();
            var runner = new AutomationSkillRunner(automation);
            result = runner.RunFile(skillPath, new PlaybackOptions { DryRun = dryRun });
        }
        catch (IOException exception)
        {
            Console.Error.WriteLine($"Could not read skill file '{skillPath}': {exception.Message}");
            return 2;
        }
        catch (UnauthorizedAccessException exception)
        {
            Console.Error.WriteLine($"Could not access skill file '{skillPath}': {exception.Message}");
            return 2;
        }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine($"Invalid skill file path '{skillPath}': {exception.Message}");
            return 2;
        }

        if (result.Error is not null)
        {
            string hResult = result.Error.HResult is int code ? $" (HRESULT 0x{code:X8})" : string.Empty;
            Console.Error.WriteLine($"Skill failed: {result.Error.Type}{hResult}: {result.Error.Message}");
            return 1;
        }

        string skillName = result.SkillName ?? Path.GetFileName(skillPath);
        Console.WriteLine($"Skill: {skillName}{(dryRun ? " (dry run)" : string.Empty)}");
        foreach (PlaybackTaskResult task in result.Playback.Tasks)
        {
            string inputStatus = dryRun || !task.InputSent ? " (input not sent)" : string.Empty;
            Console.WriteLine($"Task {task.Index + 1}: {task.Task.Action} - {task.Status}{inputStatus}");
        }

        foreach (CompletionAssertionResult assertion in result.CompletionAssertions)
        {
            string name = assertion.Assertion.Name ?? assertion.Assertion.Kind.ToString();
            Console.WriteLine($"Assertion {assertion.Index + 1}: {name} - {assertion.Status}");
        }

        if (result.WasCanceled)
        {
            Console.Error.WriteLine("Skill execution was canceled.");
        }

        return result.Succeeded ? 0 : 1;
    }

    private static bool TryParseArguments(string[] args, out string? skillPath, out bool dryRun)
    {
        skillPath = null;
        dryRun = false;

        foreach (string argument in args)
        {
            if (string.Equals(argument, "--help", StringComparison.OrdinalIgnoreCase)
                || string.Equals(argument, "-h", StringComparison.OrdinalIgnoreCase))
            {
                return args.Length == 1;
            }

            if (string.Equals(argument, "--dry-run", StringComparison.OrdinalIgnoreCase))
            {
                dryRun = true;
            }
            else if (argument.StartsWith("-", StringComparison.Ordinal) || skillPath is not null)
            {
                return false;
            }
            else
            {
                skillPath = argument;
            }
        }

        return skillPath is not null;
    }

    private static void PrintUsage() =>
        Console.WriteLine("Usage: WinUiAutomation.SkillRunner <skill.json> [--dry-run]\n       WinUiAutomation.SkillRunner --help");
}