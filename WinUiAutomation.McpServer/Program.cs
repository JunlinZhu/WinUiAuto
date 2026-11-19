using System.Text.Json;
using System.Text.Json.Serialization;
using UiAutomation.Windows;
using WinUiAutomation.Runtime;
using WindowsAutomation = UiAutomation.Windows.WinUiAutomation;

namespace WinUiAutomation.McpServer;

internal static class Program
{
    public static void Main()
    {
        using var automation = new WindowsAutomation();
        var server = new StdioMcpServer(automation);
        server.Run(Console.In, Console.Out);
    }
}

internal sealed class StdioMcpServer
{
    private readonly WindowsAutomation automation;
    private readonly AutomationSkillRunner runner;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public StdioMcpServer(WindowsAutomation automation)
    {
        this.automation = automation;
        runner = new AutomationSkillRunner(automation);
    }

    public void Run(TextReader input, TextWriter output)
    {
        string? line;
        while ((line = input.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            output.WriteLine(Handle(line));
            output.Flush();
        }
    }

    private string Handle(string line)
    {
        using JsonDocument document = JsonDocument.Parse(line);
        JsonElement request = document.RootElement;
        JsonElement id = request.TryGetProperty("id", out JsonElement requestId) ? requestId : default;
        try
        {
            string method = request.GetProperty("method").GetString() ?? throw new JsonException("method is required.");
            JsonElement parameters = request.TryGetProperty("params", out JsonElement suppliedParameters)
                ? suppliedParameters
                : EmptyObject();
            object result = method switch
            {
                "tools/list" => ToolDefinitions(),
                "get_snapshot" => GetSnapshot(parameters),
                "query_snapshot" => QuerySnapshot(parameters),
                "locate" => Locate(parameters),
                "perform_action" => PerformAction(parameters),
                "run_skill" => RunSkill(parameters),
                "get_screenshot" => GetScreenshot(parameters),
                _ => throw new JsonException($"Unknown method '{method}'.")
            };
            return JsonSerializer.Serialize(new { jsonrpc = "2.0", id, result }, JsonOptions);
        }
        catch (JsonException exception)
        {
            return JsonSerializer.Serialize(new { jsonrpc = "2.0", id, error = new { code = -32602, message = exception.Message } }, JsonOptions);
        }
        catch (UiAutomationException exception)
        {
            return JsonSerializer.Serialize(new { jsonrpc = "2.0", id, error = new { code = -32000, message = exception.Message, hresult = exception.HResult } }, JsonOptions);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or ArgumentException)
        {
            return JsonSerializer.Serialize(new { jsonrpc = "2.0", id, error = new { code = -32001, message = exception.Message } }, JsonOptions);
        }
    }

    private object GetSnapshot(JsonElement parameters) => automation.GetSnapshot(GetRootWindow(parameters), Deserialize<UiSnapshotOptions>(parameters, "options"));

    private object QuerySnapshot(JsonElement parameters)
    {
        UiSnapshotQuery query = DeserializeRequired<UiSnapshotQuery>(parameters, "query");
        return automation.QuerySnapshot(query, GetRootWindow(parameters), Deserialize<UiSnapshotOptions>(parameters, "options"));
    }

    private object Locate(JsonElement parameters)
    {
        string nodePath = parameters.GetProperty("nodePath").GetRawText();
        return automation.Locate(nodePath, Deserialize<UiLocateOptions>(parameters, "options"));
    }

    private object PerformAction(JsonElement parameters)
    {
        UiAction action = DeserializeRequired<UiAction>(parameters, "action");
        UiRectangle bounds = DeserializeRequired<UiRectangle>(parameters, "bounds");
        automation.PerformMouseAction(action, bounds, GetInt(parameters, "offsetX"), GetInt(parameters, "offsetY"), GetInt(parameters, "deltaX"), GetInt(parameters, "deltaY"));
        return new { succeeded = true };
    }

    private object RunSkill(JsonElement parameters)
    {
        JsonElement skill = parameters.GetProperty("skill");
        string skillJson = skill.ValueKind == JsonValueKind.String ? skill.GetString()! : skill.GetRawText();
        return runner.RunJson(skillJson, Deserialize<PlaybackOptions>(parameters, "playbackOptions"));
    }

    private object GetScreenshot(JsonElement parameters) => new { pngBase64 = Convert.ToBase64String(automation.GetScreenshot(GetRootWindow(parameters))) };

    private static object ToolDefinitions() => new
    {
        tools = new[]
        {
            new { name = "get_snapshot", description = "Return a bounded UI Automation tree.", inputSchema = new { type = "object" } },
            new { name = "query_snapshot", description = "Return matching UI Automation elements.", inputSchema = new { type = "object" } },
            new { name = "run_skill", description = "Validate and execute a versioned automation skill.", inputSchema = new { type = "object" } },
            new { name = "locate", description = "Resolve a node path and return its state.", inputSchema = new { type = "object" } },
            new { name = "perform_action", description = "Perform a mouse action on resolved bounds.", inputSchema = new { type = "object" } },
            new { name = "get_screenshot", description = "Capture the target window as PNG bytes.", inputSchema = new { type = "object" } }
        }
    };

    private static T? Deserialize<T>(JsonElement parameters, string property) where T : class
    {
        return parameters.TryGetProperty(property, out JsonElement value) ? value.Deserialize<T>(JsonOptions) : null;
    }

    private static T DeserializeRequired<T>(JsonElement parameters, string property)
    {
        return parameters.GetProperty(property).Deserialize<T>(JsonOptions) ?? throw new JsonException($"{property} is required.");
    }

    private static nint GetRootWindow(JsonElement parameters) => parameters.TryGetProperty("rootWindow", out JsonElement value) ? (nint)value.GetInt64() : 0;
    private static int GetInt(JsonElement parameters, string property) => parameters.TryGetProperty(property, out JsonElement value) ? value.GetInt32() : 0;
    private static JsonElement EmptyObject()
    {
        using JsonDocument document = JsonDocument.Parse("{}");
        return document.RootElement.Clone();
    }
}
