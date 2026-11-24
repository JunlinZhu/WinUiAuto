using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

string repositoryRoot = Directory.GetCurrentDirectory();
var options = HostOptions.Parse(args, repositoryRoot);
var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
{
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
};

if (!File.Exists(options.SkillPath))
{
    throw new FileNotFoundException("Skill file was not found.", options.SkillPath);
}

JsonNode skill = JsonNode.Parse(await File.ReadAllTextAsync(options.SkillPath))
    ?? throw new InvalidDataException("The skill file is empty or invalid JSON.");
AgentSkillFolder? skillFolder = options.SkillsFolderPath is string folderPath ? new AgentSkillFolder(folderPath) : null;

await using var mcp = new McpProcess(options.McpProjectPath, options.Configuration, options.Platform);
using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
var tools = await mcp.ListToolsAsync(jsonOptions);
if (skillFolder is not null)
{
    tools.AsArray().Add(skillFolder.ToolDefinition());
}
tools.AsArray().Add(new JsonObject
{
    ["type"] = "function",
    ["function"] = new JsonObject
    {
        ["name"] = "load_source_skill",
        ["description"] = "Read the complete recorded source JSON only when needed for editing or non-Calculator tasks. Calculator button sequences do not need this large payload.",
        ["parameters"] = new JsonObject { ["type"] = "object", ["properties"] = new JsonObject() }
    }
});

Console.WriteLine("Ollama MCP host is ready.");
Console.WriteLine($"Skill: {options.SkillPath}");
if (skillFolder is not null)
{
    Console.WriteLine($"Skill instructions folder: {options.SkillsFolderPath} ({skillFolder.Paths.Count} Markdown files)");
    Console.WriteLine("Ollama will select relevant instructions; the deterministic Calculator shortcut is disabled.");
}
Console.WriteLine("Type a request such as: 45+67 or compute 45+67");
Console.WriteLine("Type /quit to exit.");

while (true)
{
    Console.Write(">>> ");
    string? request = Console.ReadLine();
    if (request is null || request.Equals("/quit", StringComparison.OrdinalIgnoreCase))
    {
        break;
    }

    if (string.IsNullOrWhiteSpace(request))
    {
        continue;
    }

    await RunConversationAsync(request, skill, tools, options, mcp, http, jsonOptions, skillFolder);
}

static async Task RunConversationAsync(
    string request,
    JsonNode skill,
    JsonNode tools,
    HostOptions options,
    McpProcess mcp,
    HttpClient http,
    JsonSerializerOptions jsonOptions,
    AgentSkillFolder? skillFolder)
{
    if (skillFolder is null && TryGetExpression(request, out string directExpression))
    {
        await RunCalculatorFallbackAsync(directExpression, skill, mcp, jsonOptions);
        return;
    }

    bool retriedEmptyResponse = false;
    bool useCalculatorShorthand = IsCalculatorSkill(skill);
    var messages = new JsonArray
    {
        new JsonObject
        {
            ["role"] = "system",
            ["content"] = "You are a Windows UI automation agent. The host has loaded a recorded skill. " +
                "Use the exact recorded task targets; never invent simplified paths. " +
                (useCalculatorShorthand
                    ? "For Calculator requests, use calculatorExpression for basic arithmetic or calculatorButtons for scientific operations; " +
                        "calculatorExpression accepts only numbers and +, -, *, /. Never send function notation such as ln(10) " +
                        "through calculatorExpression; use calculatorButtons instead (ln(10) is num1Button,num0Button,logBaseEButton,equalButton). " +
                        "The host derives the complete skill from recorded targets. First inspect the desktop, then call run_skill " +
                        "with playbackOptions.dryRun=true. After the dry-run result, report it briefly and stop; do not repeat the run. "
                    : "First inspect the desktop, then run the complete supplied skill with dryRun=true. " +
                        "For a changed Calculator expression, derive a complete skill preserving launch, navigation, result, and cleanup " +
                        "tasks; use recorded button target templates and include the Equals task. ") +
                "After a successful dry run, ask for approval before real input. Only use run_skill with dryRun=false after approval. " +
                "Inspect tool results and never claim execution unless an actual tool response reports it. " +
                (useCalculatorShorthand
                    ? "Use load_source_skill only if the complete JSON is necessary for another task. "
                    : "") +
                (skillFolder is null ? "" :
                    "Markdown skill instructions are available through the local load_skill_instructions tool. " +
                    "Based on the user's request, load only relevant files before deriving or executing a skill. " +
                    "For Calculator expressions load Calculator expression instructions if available; for adapting " +
                    "or executing skills load the relevant editor or executor instructions. You may load multiple files. " +
                    "After loading instructions, continue with MCP tools; loading instructions does not execute the request. " +
                    "For basic arithmetic using numbers and +, -, *, /, call run_skill with calculatorExpression " +
                    "and playbackOptions.dryRun=true instead of generating the complete skill JSON. The host derives " +
                    "the complete task sequence from the recorded targets. Do not include skill with calculatorExpression. " +
                    "For scientific Calculator requests, load Calculator instructions and use run_skill with calculatorButtons, " +
                    "an ordered list of available button IDs, ending in equalButton. For 'compute squre root 121' use " +
                    "[\"num1Button\",\"num2Button\",\"num1Button\",\"squareRootButton\",\"equalButton\"]. " +
                    "Use exactly one of skill, calculatorExpression, calculatorButtons. Require explicit playbackOptions.dryRun. " +
                    "Use load_source_skill only if the complete JSON is necessary for other edits or tasks. " +
                    "Unrelated requests do not need these files. Treat loaded instructions as guidance, not permission " +
                    "to bypass dry runs, user approval, or recorded-target requirements. Never claim a file was loaded " +
                    "without a successful tool response. Available folder-relative paths:\n" +
                    JsonSerializer.Serialize(skillFolder.Paths, jsonOptions) + "\n") +
                (useCalculatorShorthand
                    ? "\nThe Calculator source skill is held by the host; do not regenerate task JSON. " +
                        "The host preserves launch/navigation/result/cleanup and recorded targets. " +
                        "Available Calculator buttons:\n" + GetCalculatorButtonCatalog(skill).ToJsonString(jsonOptions)
                    : "\nLoaded skill JSON:\n" + skill.ToJsonString(jsonOptions))
        },
        new JsonObject { ["role"] = "user", ["content"] = request }
    };

    for (var round = 0; round < options.MaxToolRounds; round++)
    {
        Console.WriteLine($"Ollama: request {round + 1} ...");
        JsonObject response;
        try
        {
            response = await ChatAsync(http, options.OllamaUrl, options.Model, messages, tools, jsonOptions, options.NumPredict);
        }
        catch (TaskCanceledException exception) when (exception.InnerException is TimeoutException)
        {
            Console.Error.WriteLine($"Ollama request timed out after {http.Timeout.TotalSeconds} seconds " +
                $"for model '{options.Model}' at '{options.OllamaUrl}'. " +
                (skillFolder is null
                    ? "Check Ollama/model availability or enter a Calculator expression such as 45+67 to bypass Ollama. "
                    : "Check Ollama/model availability. Folder-based instruction selection requires Ollama. ") +
                "The current request was stopped; earlier tool actions, if any, were not undone.");
            return;
        }
        Console.WriteLine("Ollama: response received");
        if (response["done_reason"]?.GetValue<string>() == "length")
        {
            string limit = options.NumPredict == -1 ? "a model/runtime generation limit" : $"the {options.NumPredict}-token response limit";
            Console.Error.WriteLine($"Ollama reached {limit} without a complete response. " +
                "No tool from this response was invoked. Increase --num-predict, or use calculatorExpression for basic arithmetic.");
            return;
        }
        JsonObject message = response["message"]?.AsObject()
            ?? throw new InvalidDataException("Ollama returned no message.");
        messages.Add(message.DeepClone());

        JsonArray? toolCalls = message["tool_calls"]?.AsArray();
        if (toolCalls is null || toolCalls.Count == 0)
        {
            string content = message["content"]?.GetValue<string>() ?? "";
            string thinking = message["thinking"]?.GetValue<string>() ?? "";
            if (string.IsNullOrWhiteSpace(content))
            {
                if (string.IsNullOrWhiteSpace(thinking))
                {
                    Console.Error.WriteLine("Ollama returned an empty response without a tool call. No tool from this response was invoked.");
                    if (!retriedEmptyResponse && round + 1 < options.MaxToolRounds)
                    {
                        retriedEmptyResponse = true;
                        messages.Add(new JsonObject
                        {
                            ["role"] = "user",
                            ["content"] = "Your last response was empty. Continue the original request using native tool calls, " +
                                "or explain what prevents proceeding. Loading instruction files alone is not execution. " +
                                "Do not repeat earlier UI actions or bypass dry runs and approval."
                        });
                        Console.WriteLine("Ollama: retrying the empty response once ...");
                        continue;
                    }
                    return;
                }
                Console.Error.WriteLine("Ollama returned thinking without a tool call or final response. " +
                    "Use a model/runtime configuration with thinking disabled or a shorter reasoning budget.");
            }
            else
            {
                Console.WriteLine(content);
            }
            return;
        }

        foreach (JsonNode? callNode in toolCalls)
        {
            JsonObject call = callNode?.AsObject() ?? throw new InvalidDataException("Ollama returned an invalid tool call.");
            JsonObject function = call["function"]?.AsObject()
                ?? throw new InvalidDataException("Ollama returned a tool call without a function.");
            string name = function["name"]?.GetValue<string>()
                ?? throw new InvalidDataException("Ollama returned a tool call without a name.");
            JsonObject arguments = ParseArguments(function["arguments"]);

            Console.WriteLine($"Tool: {name}");
            JsonNode result;
            try
            {
                result = await InvokeToolAsync(name, arguments, mcp, jsonOptions, skillFolder, skill);
            }
            catch (Exception exception)
            {
                result = new JsonObject { ["succeeded"] = false, ["error"] = exception.Message };
                Console.Error.WriteLine($"Tool {name} failed: {exception.Message}");
            }

            messages.Add(new JsonObject
            {
                ["role"] = "tool",
                ["tool_name"] = name,
                ["content"] = result.ToJsonString(jsonOptions)
            });
        }
    }

    throw new InvalidOperationException($"Ollama exceeded {options.MaxToolRounds} tool rounds.");
}

static bool TryGetExpression(string request, out string expression)
{
    System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(
        request, @"^\s*(?:compute\s+)?([0-9+*/.\- ]+)\s*$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    expression = match.Success ? match.Groups[1].Value.Replace(" ", "") : "";
    return match.Success && expression.Length > 0;
}

static bool TryGetBasicCalculatorExpression(string request, out string expression)
{
    return TryGetExpression(request, out expression) && System.Text.RegularExpressions.Regex.IsMatch(
        expression, @"^-?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)(?:[+\-*/]-?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+))*$");
}

static async Task RunCalculatorFallbackAsync(string expression, JsonNode sourceSkill, McpProcess mcp, JsonSerializerOptions jsonOptions)
{
    JsonObject derived = BuildCalculatorSkill(sourceSkill, expression);
    var launch = new JsonObject
    {
        ["skill"] = new JsonObject
        {
            ["format"] = derived["format"]?.DeepClone(),
            ["schemaVersion"] = derived["schemaVersion"]?.DeepClone(),
            ["tasks"] = new JsonArray(derived["tasks"]!.AsArray().Take(2).Select(task => task!.DeepClone()).ToArray())
        },
        ["playbackOptions"] = new JsonObject { ["dryRun"] = true }
    };
    Console.WriteLine("Using the deterministic Calculator skill fallback (no Ollama request).");
    JsonNode preflight = await mcp.CallAsync("run_skill", launch, jsonOptions);
    Console.WriteLine($"MCP dry run: {preflight["succeeded"]?.GetValue<bool>()}");
    if (preflight["succeeded"]?.GetValue<bool>() != true)
    {
        Console.WriteLine(preflight.ToJsonString(jsonOptions));
        return;
    }

    Console.Write("Type RUN to send real input: ");
    if (!string.Equals(Console.ReadLine()?.Trim(), "RUN", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("Execution canceled.");
        return;
    }

    var execution = new JsonObject
    {
        ["skill"] = derived.DeepClone(),
        ["playbackOptions"] = new JsonObject { ["dryRun"] = false }
    };
    JsonNode result = await mcp.CallAsync("run_skill", execution, jsonOptions);
    Console.WriteLine(result.ToJsonString(jsonOptions));
}

static JsonObject BuildCalculatorSkill(JsonNode sourceSkill, string expression)
{
    var ids = new Dictionary<char, string>
    {
        ['+'] = "plusButton", ['-'] = "minusButton", ['*'] = "multiplyButton", ['/'] = "divideButton",
        ['.'] = "decimalButton", ['='] = "equalButton"
    };
    return BuildCalculatorButtonSkill(sourceSkill, (expression + "=").Select(character =>
        char.IsAsciiDigit(character) ? $"num{character}Button" : ids[character]).ToArray());
}

static Dictionary<string, JsonObject> GetRecordedTargets(JsonNode sourceSkill)
{
    var byAutomationId = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
    foreach (JsonNode? taskNode in sourceSkill["tasks"]!.AsArray())
    {
        JsonObject task = taskNode!.AsObject();
        string? id = task["target"]?.AsArray().LastOrDefault()?["automationId"]?.GetValue<string>();
        if (id is not null) byAutomationId.TryAdd(id, task);
    }
    return byAutomationId;
}

static bool IsCalculatorSkill(JsonNode skill)
{
    var targets = GetRecordedTargets(skill);
    return targets.ContainsKey("num1Button") && targets.ContainsKey("plusButton") &&
        targets.ContainsKey("equalButton") && targets.ContainsKey("CalculatorResults") && skill["tasks"]!.AsArray().Count >= 4;
}

static Dictionary<string, string> GetBasicCalculatorButtons() => new(StringComparer.Ordinal)
{
    ["num0Button"] = "Zero", ["num1Button"] = "One", ["num2Button"] = "Two", ["num3Button"] = "Three",
    ["num4Button"] = "Four", ["num5Button"] = "Five", ["num6Button"] = "Six", ["num7Button"] = "Seven",
    ["num8Button"] = "Eight", ["num9Button"] = "Nine", ["plusButton"] = "Plus", ["minusButton"] = "Minus",
    ["multiplyButton"] = "Multiply", ["divideButton"] = "Divide", ["decimalButton"] = "Decimal separator", ["equalButton"] = "Equals"
};

static JsonArray GetCalculatorButtonCatalog(JsonNode sourceSkill)
{
    var names = GetBasicCalculatorButtons();
    foreach (var (id, task) in GetRecordedTargets(sourceSkill))
    {
        JsonObject leaf = task["target"]!.AsArray().Last()!.AsObject();
        if (leaf["controlType"]?.GetValue<string>() == "Button" && id.EndsWith("Button", StringComparison.Ordinal))
        {
            names[id] = leaf["name"]?.GetValue<string>() ?? id;
        }
    }
    return new JsonArray(names.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => (JsonNode)new JsonObject
    {
        ["id"] = pair.Key, ["name"] = pair.Value
    }).ToArray());
}

static JsonObject BuildCalculatorButtonSkill(JsonNode sourceSkill, string[] buttons)
{
    if (!IsCalculatorSkill(sourceSkill))
    {
        throw new InvalidDataException("Calculator shorthand requires a recorded Calculator skill with launch/navigation, number, operator, Equals, and result templates.");
    }
    if (buttons.Length == 0 || buttons.Last() != "equalButton")
    {
        throw new JsonException("calculatorButtons must contain a nonempty ordered sequence ending in equalButton.");
    }
    JsonObject derived = sourceSkill.AsObject().DeepClone().AsObject();
    JsonArray tasks = derived["tasks"]!.AsArray();
    var byAutomationId = GetRecordedTargets(derived);
    var names = GetBasicCalculatorButtons();
    var available = GetCalculatorButtonCatalog(derived).Select(node => node!["id"]!.GetValue<string>()).ToHashSet(StringComparer.Ordinal);
    var clicks = new JsonArray();
    foreach (string id in buttons)
    {
        if (!available.Contains(id))
        {
            throw new JsonException($"Calculator button '{id}' is unavailable. Use only the supplied button catalog; additional scientific targets must be recorded.");
        }
        string templateId = id is "plusButton" or "minusButton" or "multiplyButton" or "divideButton"
            ? "plusButton" : id == "equalButton" ? "equalButton" : "num1Button";
        JsonObject template = byAutomationId[templateId];
        JsonObject task = template.DeepClone().AsObject();
        JsonObject leaf = task["target"]!.AsArray().Last()!.AsObject();
        if (byAutomationId.TryGetValue(id, out JsonObject? recordedTask))
        {
            task["target"] = recordedTask["target"]!.DeepClone();
            leaf = task["target"]!.AsArray().Last()!.AsObject();
        }
        else
        {
            leaf["automationId"] = id;
            leaf["name"] = names[id];
        }
        foreach (string property in new[] { "position", "x", "y", "w", "h", "lx", "ly" }) leaf.Remove(property);
        clicks.Add(task);
    }

    int resultIndex = tasks.IndexOf(byAutomationId["CalculatorResults"]);
    var replacement = new JsonArray();
    for (int i = 0; i < 4; i++) replacement.Add(tasks[i]!.DeepClone());
    foreach (JsonNode? click in clicks) replacement.Add(click!.DeepClone());
    JsonObject resultTask = tasks[resultIndex]!.DeepClone().AsObject();
    resultTask["target"]!.AsArray().Last()!.AsObject().Remove("name");
    replacement.Add(resultTask);
    if (byAutomationId.TryGetValue("Close", out JsonObject? closeTask))
    {
        replacement.Add(closeTask.DeepClone());
    }
    derived["tasks"] = replacement;
    return derived;
}

static async Task<JsonNode> InvokeToolAsync(
    string name,
    JsonObject arguments,
    McpProcess mcp,
    JsonSerializerOptions jsonOptions,
    AgentSkillFolder? skillFolder,
    JsonNode sourceSkill)
{
    if (name == "load_source_skill")
    {
        Console.WriteLine("Loaded complete recorded source skill.");
        return sourceSkill.DeepClone();
    }
    if (name == AgentSkillFolder.ToolName)
    {
        if (skillFolder is null)
        {
            throw new InvalidOperationException("Use --skills-folder to enable Markdown skill instructions.");
        }
        string path = arguments["path"]?.GetValue<string>()
            ?? throw new JsonException("load_skill_instructions requires a folder-relative path.");
        return await skillFolder.LoadAsync(path);
    }

    if (name == "run_skill" && (arguments.ContainsKey("calculatorExpression") || arguments.ContainsKey("calculatorButtons")))
    {
        if (arguments.ContainsKey("skill") || (arguments.ContainsKey("calculatorExpression") && arguments.ContainsKey("calculatorButtons")))
        {
            throw new JsonException("run_skill accepts exactly one of skill, calculatorExpression, calculatorButtons.");
        }
        if (arguments["playbackOptions"]?["dryRun"] is not JsonValue dryRun || !dryRun.TryGetValue<bool>(out _))
        {
            throw new JsonException("Calculator shorthand requires an explicit boolean playbackOptions.dryRun.");
        }
        JsonObject derived;
        if (arguments.ContainsKey("calculatorButtons"))
        {
            JsonArray buttons = arguments["calculatorButtons"] as JsonArray
                ?? throw new JsonException("calculatorButtons requires an array of button ID strings.");
            derived = BuildCalculatorButtonSkill(sourceSkill, buttons.Select(button => button?.GetValue<string>()
                ?? throw new JsonException("calculatorButtons cannot contain null IDs.")).ToArray());
        }
        else
        {
            string expression = arguments["calculatorExpression"]?.GetValue<string>()
                ?? throw new JsonException("calculatorExpression requires a basic arithmetic expression.");
            if (!TryGetBasicCalculatorExpression(expression, out string normalized))
            {
                throw new JsonException("calculatorExpression supports numbers and +, -, *, / only. Use calculatorButtons or a complete skill for scientific expressions.");
            }
            derived = BuildCalculatorSkill(sourceSkill, normalized);
        }
        arguments = arguments.DeepClone().AsObject();
        arguments.Remove("calculatorExpression");
        arguments.Remove("calculatorButtons");
        arguments["skill"] = derived;
    }

    if (name == "run_skill" && IsRealExecution(arguments))
    {
        Console.Write("The agent requested real UI input. Type RUN to approve: ");
        if (!string.Equals(Console.ReadLine()?.Trim(), "RUN", StringComparison.OrdinalIgnoreCase))
        {
            return new JsonObject { ["succeeded"] = false, ["wasCanceled"] = true, ["error"] = "User declined real input." };
        }
    }

    Console.WriteLine($"MCP: {name}");
    return await mcp.CallAsync(name, arguments, jsonOptions);
}

static bool IsRealExecution(JsonObject arguments)
{
    return arguments["playbackOptions"]?["dryRun"]?.GetValue<bool>() == false;
}

static JsonObject ParseArguments(JsonNode? arguments)
{
    if (arguments is JsonObject objectArguments)
    {
        return objectArguments;
    }

    if (arguments is JsonValue stringValue && stringValue.TryGetValue<string>(out string? text))
    {
        return JsonNode.Parse(text)?.AsObject() ?? throw new InvalidDataException("Tool arguments are not a JSON object.");
    }

    return new JsonObject();
}

static async Task<JsonObject> ChatAsync(
    HttpClient http,
    string ollamaUrl,
    string model,
    JsonArray messages,
    JsonNode tools,
    JsonSerializerOptions jsonOptions,
    int numPredict)
{
    var payload = new JsonObject
    {
        ["model"] = model,
        ["messages"] = messages.DeepClone(),
        ["tools"] = tools.DeepClone(),
        ["stream"] = false,
        ["think"] = false,
        ["options"] = new JsonObject
        {
            ["num_predict"] = numPredict,
            ["temperature"] = 0
        }
    };

    using HttpResponseMessage response = await http.PostAsJsonAsync(
        $"{ollamaUrl.TrimEnd('/')}/api/chat", payload, jsonOptions);
    string body = await response.Content.ReadAsStringAsync();
    if (!response.IsSuccessStatusCode)
    {
        throw new InvalidOperationException($"Ollama returned {(int)response.StatusCode}: {body}");
    }

    return JsonNode.Parse(body)?.AsObject()
        ?? throw new InvalidDataException("Ollama returned invalid JSON.");
}

internal sealed class AgentSkillFolder
{
    public const string ToolName = "load_skill_instructions";
    private readonly Dictionary<string, string> files;
    public IReadOnlyList<string> Paths { get; }

    public AgentSkillFolder(string folderPath)
    {
        if (!Directory.Exists(folderPath))
        {
            throw new DirectoryNotFoundException($"Skill instructions folder was not found: {folderPath}");
        }

        files = Directory.EnumerateFiles(folderPath, "*.md", new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = false,
            AttributesToSkip = FileAttributes.ReparsePoint,
            MatchCasing = MatchCasing.CaseInsensitive
        }).ToDictionary(path => Path.GetRelativePath(folderPath, path), path => Path.GetFullPath(path), StringComparer.OrdinalIgnoreCase);
        Paths = files.Keys.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
        if (Paths.Count == 0)
        {
            throw new InvalidDataException($"Skill instructions folder contains no Markdown files: {folderPath}");
        }
    }

    public JsonObject ToolDefinition() => new()
    {
        ["type"] = "function",
        ["function"] = new JsonObject
        {
            ["name"] = ToolName,
            ["description"] = "Load a Markdown skill's instructions when relevant to the current user request. Choose a listed folder-relative path. This tool reads instructions only; it does not execute UI actions.",
            ["parameters"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["path"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["enum"] = new JsonArray(Paths.Select(path => (JsonNode?)JsonValue.Create(path)).ToArray())
                    }
                },
                ["required"] = new JsonArray("path"),
                ["additionalProperties"] = false
            }
        }
    };

    public async Task<JsonNode> LoadAsync(string path)
    {
        if (!files.TryGetValue(path, out string? filePath))
        {
            throw new ArgumentException($"Unknown skill instruction path '{path}'. Choose a path from the supplied catalog.", nameof(path));
        }
        string content = await File.ReadAllTextAsync(filePath);
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidDataException($"Skill instruction file is empty: {path}");
        }
        Console.WriteLine($"Loaded skill instructions: {path}");
        return new JsonObject { ["path"] = path, ["content"] = content };
    }
}

internal sealed class McpProcess : IAsyncDisposable
{
    private readonly Process process;
    private readonly Task<string> standardError;
    private int nextId;

    public McpProcess(string projectPath, string configuration, string platform)
    {
        process = Process.Start(new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = Directory.GetCurrentDirectory(),
            Arguments = $"run --project \"{projectPath}\" -c {configuration} -p:Platform={platform} --no-build",
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        }) ?? throw new InvalidOperationException("Could not start WinUiAutomation.McpServer.");
        standardError = ReadStandardErrorAsync();
    }

    private async Task<string> ReadStandardErrorAsync()
    {
        var output = new System.Text.StringBuilder();
        while (await process.StandardError.ReadLineAsync() is string line)
        {
            Console.Error.WriteLine(line);
            output.AppendLine(line);
        }
        return output.ToString();
    }

    public async Task<JsonNode> ListToolsAsync(JsonSerializerOptions jsonOptions)
    {
        JsonNode result = await CallAsync("tools/list", new JsonObject(), jsonOptions);
        JsonArray mcpTools = result["tools"]?.AsArray()
            ?? throw new InvalidDataException("MCP tools/list returned no tools.");
        var ollamaTools = new JsonArray();
        foreach (JsonNode? mcpToolNode in mcpTools)
        {
            JsonObject mcpTool = mcpToolNode?.AsObject()
                ?? throw new InvalidDataException("MCP returned an invalid tool definition.");
            ollamaTools.Add(new JsonObject
            {
                ["type"] = "function",
                ["function"] = new JsonObject
                {
                    ["name"] = mcpTool["name"]?.DeepClone(),
                    ["description"] = mcpTool["description"]?.DeepClone(),
                    ["parameters"] = ToolSchema(mcpTool["name"]?.GetValue<string>() ?? "")
                }
            });
        }

        return ollamaTools;
    }

    private static JsonObject ToolSchema(string name)
    {
        JsonObject schema = new() { ["type"] = "object", ["properties"] = new JsonObject() };
        JsonObject properties = schema["properties"]!.AsObject();
        switch (name)
        {
            case "query_snapshot":
                properties["query"] = new JsonObject { ["type"] = "object" };
                properties["options"] = new JsonObject { ["type"] = "object" };
                break;
            case "locate":
                properties["nodePath"] = new JsonObject { ["type"] = "array" };
                properties["options"] = new JsonObject { ["type"] = "object" };
                break;
            case "perform_action":
                properties["action"] = new JsonObject { ["type"] = "object" };
                properties["bounds"] = new JsonObject { ["type"] = "object" };
                properties["offsetX"] = new JsonObject { ["type"] = "integer" };
                properties["offsetY"] = new JsonObject { ["type"] = "integer" };
                properties["deltaX"] = new JsonObject { ["type"] = "integer" };
                properties["deltaY"] = new JsonObject { ["type"] = "integer" };
                break;
            case "run_skill":
                properties["skill"] = new JsonObject { ["type"] = "object" };
                properties["calculatorExpression"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = "Basic arithmetic expression, e.g. 45+67. The host derives the complete skill from recorded targets. Use this OR skill, not both. Supports numbers and +, -, *, / only; no powers or scientific functions. Requires explicit playbackOptions.dryRun."
                };
                properties["calculatorButtons"] = new JsonObject
                {
                    ["type"] = "array",
                    ["items"] = new JsonObject { ["type"] = "string" },
                    ["minItems"] = 1,
                    ["description"] = "Ordered Calculator button IDs from the supplied catalog, ending in equalButton. For ln(10): num1Button,num0Button,logBaseEButton,equalButton. For square root of 121: num1Button,num2Button,num1Button,squareRootButton,equalButton. Host preserves recorded paths and launch/result/cleanup. Use this OR calculatorExpression OR skill, with explicit playbackOptions.dryRun."
                };
                properties["playbackOptions"] = new JsonObject { ["type"] = "object" };
                break;
        }

        return schema;
    }

    public async Task<JsonNode> CallAsync(string method, JsonObject parameters, JsonSerializerOptions jsonOptions)
    {
        int id = Interlocked.Increment(ref nextId);
        var request = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["method"] = method,
            ["params"] = parameters.DeepClone()
        };
        await process.StandardInput.WriteLineAsync(request.ToJsonString(jsonOptions));
        await process.StandardInput.FlushAsync();

        string? line = await process.StandardOutput.ReadLineAsync();
        if (line is null)
        {
            await process.WaitForExitAsync();
            string startupError = await standardError;
            throw new InvalidOperationException(
                $"The MCP server exited with code {process.ExitCode} without returning a response. " +
                "Build WinUiAutomation.McpServer with the host's --configuration and --platform before running with --no-build." +
                (string.IsNullOrWhiteSpace(startupError) ? "" : Environment.NewLine + startupError.TrimEnd()));
        }

        JsonObject response = JsonNode.Parse(line)?.AsObject()
            ?? throw new InvalidDataException("MCP returned invalid JSON.");
        if (response["error"] is JsonNode error)
        {
            throw new InvalidOperationException(error.ToJsonString(jsonOptions));
        }

        return response["result"]?.DeepClone() ?? new JsonObject();
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await process.StandardInput.FlushAsync();
            process.StandardInput.Close();
        }
        catch (InvalidOperationException)
        {
        }

        if (!process.HasExited)
        {
            process.Kill(true);
            await process.WaitForExitAsync();
        }
        await standardError;
        process.Dispose();
    }
}

internal sealed record HostOptions(
    string Model,
    string SkillPath,
    string McpProjectPath,
    string OllamaUrl,
    string Configuration,
    string Platform,
    int MaxToolRounds,
    string? SkillsFolderPath,
    int NumPredict)
{
    public static HostOptions Parse(string[] args, string repositoryRoot)
    {
        string model = Get(args, "--model", "qwen3");
        string skill = Get(args, "--skill", Path.Combine(repositoryRoot, "Samples", "Calculator.winui-skill.json"));
        string mcp = Get(args, "--mcp-project", Path.Combine(repositoryRoot, "WinUiAutomation.McpServer", "WinUiAutomation.McpServer.csproj"));
        string ollama = Get(args, "--ollama-url", "http://localhost:11434");
        string configuration = Get(args, "--configuration", "Release");
        string platform = Get(args, "--platform", "x64");
        int rounds = int.Parse(Get(args, "--max-tool-rounds", "16"));
        int predictionIndex = Array.IndexOf(args, "--num-predict");
        int numPredict = 8192;
        if (predictionIndex >= 0 && (predictionIndex + 1 >= args.Length ||
            !int.TryParse(args[predictionIndex + 1], out numPredict) || (numPredict <= 0 && numPredict != -1)))
        {
            throw new ArgumentException("--num-predict requires a positive integer token limit or -1 for no explicit output limit.");
        }
        int folderIndex = Array.IndexOf(args, "--skills-folder");
        string? skillsFolder = null;
        if (folderIndex >= 0)
        {
            if (folderIndex + 1 >= args.Length || string.IsNullOrWhiteSpace(args[folderIndex + 1]) || args[folderIndex + 1].StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException("--skills-folder requires a folder path.");
            }
            skillsFolder = Path.GetFullPath(args[folderIndex + 1]);
        }
        return new HostOptions(
            model,
            Path.GetFullPath(skill),
            Path.GetFullPath(mcp),
            ollama,
            configuration,
            platform,
            rounds,
            skillsFolder,
            numPredict);
    }

    private static string Get(string[] args, string name, string fallback)
    {
        int index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
    }
}
