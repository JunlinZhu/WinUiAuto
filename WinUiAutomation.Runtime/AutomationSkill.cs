using System.Text.Json;
using System.Text.Json.Serialization;

namespace WinUiAutomation.Runtime;

public sealed record AutomationSkill
{
    public const string CurrentFormat = "win-ui-automation-skill";
    public const int CurrentSchemaVersion = 2;

    public string Format { get; init; } = CurrentFormat;
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public string? Name { get; init; }
    public string? Description { get; init; }
    public required IReadOnlyList<AutomationSkillTask> Tasks { get; init; }
    public IReadOnlyList<AutomationCompletionAssertion> CompletionAssertions { get; init; } = Array.Empty<AutomationCompletionAssertion>();

    public AutomationSequence ToSequence() => new()
    {
        Name = Name,
        Tasks = Tasks.Select(task => task.ToAutomationTask()).ToArray()
    };

    public static AutomationSkill FromSequence(AutomationSequence sequence, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(sequence);
        ArgumentNullException.ThrowIfNull(sequence.Tasks);

        return new AutomationSkill
        {
            Name = sequence.Name,
            Description = description,
            Tasks = sequence.Tasks.Select(AutomationSkillTask.FromAutomationTask).ToArray()
        };
    }
}

public enum AutomationCompletionAssertionKind
{
    ElementExists,
    ElementNotExists
}

public sealed record AutomationCompletionAssertion
{
    public string? Name { get; init; }
    public required AutomationCompletionAssertionKind Kind { get; init; }
    public required JsonElement Target { get; init; }
    public int TimeoutMilliseconds { get; init; } = 5000;
    public int PollIntervalMilliseconds { get; init; } = 250;

    internal string GetNodePath()
    {
        Validate();
        return Target.GetRawText();
    }

    internal void Validate()
    {
        if (!Enum.IsDefined(Kind))
        {
            throw new JsonException($"Unsupported completion assertion kind '{Kind}'.");
        }

        if (Target.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("A completion assertion target must be a JSON array.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(TimeoutMilliseconds);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(PollIntervalMilliseconds);
    }
}

public sealed record AutomationSkillTask
{
    public required AutomationTaskAction Action { get; init; }
    public JsonElement? Target { get; init; }
    public string? KeyboardInputBase64 { get; init; }
    public bool CapsLock { get; init; }
    public bool NumLock { get; init; }
    public bool ScrollLock { get; init; }
    public int OffsetX { get; init; }
    public int OffsetY { get; init; }
    public int DeltaX { get; init; }
    public int DeltaY { get; init; }
    public int DelayBeforeMilliseconds { get; init; }

    public AutomationTask ToAutomationTask()
    {
        Validate();
        return new AutomationTask
        {
            Action = Action,
            NodePath = Target?.GetRawText(),
            KeyboardInput = KeyboardInputBase64,
            CapsLock = CapsLock,
            NumLock = NumLock,
            ScrollLock = ScrollLock,
            OffsetX = OffsetX,
            OffsetY = OffsetY,
            DeltaX = DeltaX,
            DeltaY = DeltaY,
            DelayBefore = TimeSpan.FromMilliseconds(DelayBeforeMilliseconds)
        };
    }

    internal static AutomationSkillTask FromAutomationTask(AutomationTask task)
    {
        ArgumentNullException.ThrowIfNull(task);
        JsonElement? target = null;
        if (!string.IsNullOrWhiteSpace(task.NodePath))
        {
            using JsonDocument document = JsonDocument.Parse(task.NodePath);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                throw new ArgumentException("A task node path must be a JSON array.", nameof(task));
            }

            target = document.RootElement.Clone();
        }

        int delayMilliseconds = checked((int)task.DelayBefore.TotalMilliseconds);
        var skillTask = new AutomationSkillTask
        {
            Action = task.Action,
            Target = target,
            KeyboardInputBase64 = task.KeyboardInput,
            CapsLock = task.CapsLock,
            NumLock = task.NumLock,
            ScrollLock = task.ScrollLock,
            OffsetX = task.OffsetX,
            OffsetY = task.OffsetY,
            DeltaX = task.DeltaX,
            DeltaY = task.DeltaY,
            DelayBeforeMilliseconds = delayMilliseconds
        };
        skillTask.Validate();
        return skillTask;
    }

    private void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegative(DelayBeforeMilliseconds);
        if (!Enum.IsDefined(Action))
        {
            throw new JsonException($"Unsupported task action '{Action}'.");
        }

        if (Action == AutomationTaskAction.KeyboardInput)
        {
            ArgumentException.ThrowIfNullOrEmpty(KeyboardInputBase64);
            try
            {
                Convert.FromBase64String(KeyboardInputBase64);
            }
            catch (FormatException exception)
            {
                throw new JsonException("keyboardInputBase64 must contain valid Base64 data.", exception);
            }
        }
        else if (Action != AutomationTaskAction.MouseWheel && Target is null)
        {
            throw new JsonException($"Action {Action} requires a target node path.");
        }

        if (Target is JsonElement element && element.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("A task target must be a JSON array.");
        }
    }
}

public static class AutomationSkillJson
{
    private static readonly JsonSerializerOptions s_options = CreateOptions();

    public static string Serialize(AutomationSkill skill)
    {
        Validate(skill);
        return JsonSerializer.Serialize(skill, s_options);
    }

    public static AutomationSkill Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        using (JsonDocument document = JsonDocument.Parse(json))
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("format", out _)
                || !document.RootElement.TryGetProperty("schemaVersion", out _))
            {
                throw new JsonException("A skill document must declare format and schemaVersion.");
            }
        }

        AutomationSkill skill = JsonSerializer.Deserialize<AutomationSkill>(json, s_options)
            ?? throw new JsonException("The skill document is empty.");
        Validate(skill);
        return skill;
    }

    private static void Validate(AutomationSkill skill)
    {
        ArgumentNullException.ThrowIfNull(skill);
        if (!string.Equals(skill.Format, AutomationSkill.CurrentFormat, StringComparison.Ordinal))
        {
            throw new JsonException($"Unsupported skill format '{skill.Format}'.");
        }

        if (skill.SchemaVersion is < 1 or > AutomationSkill.CurrentSchemaVersion)
        {
            throw new JsonException($"Unsupported skill schema version {skill.SchemaVersion}. Supported versions are 1 through {AutomationSkill.CurrentSchemaVersion}.");
        }

        ArgumentNullException.ThrowIfNull(skill.Tasks);
        if (skill.Tasks.Count == 0)
        {
            throw new JsonException("A skill must contain at least one task.");
        }

        foreach (AutomationSkillTask task in skill.Tasks)
        {
            ArgumentNullException.ThrowIfNull(task);
            _ = task.ToAutomationTask();
        }

        ArgumentNullException.ThrowIfNull(skill.CompletionAssertions);
        if (skill.SchemaVersion == 1 && skill.CompletionAssertions.Count != 0)
        {
            throw new JsonException("Completion assertions require skill schema version 2.");
        }

        foreach (AutomationCompletionAssertion assertion in skill.CompletionAssertions)
        {
            ArgumentNullException.ThrowIfNull(assertion);
            assertion.Validate();
        }
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        return options;
    }
}
