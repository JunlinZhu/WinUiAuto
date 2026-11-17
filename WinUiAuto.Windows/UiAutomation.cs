using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace UiAutomation.Windows;

public enum PathDirection
{
    RootToLeaf,
    LeafToRoot
}

public enum UiAction
{
    DefaultInvoke,
    LeftClick,
    RightClick,
    LeftDoubleClick,
    Drag,
    MouseWheel,
    MouseHover
}

public readonly record struct UiRectangle(int Left, int Top, int Right, int Bottom)
{
    public int Width => Math.Max(0, Right - Left);
    public int Height => Math.Max(0, Bottom - Top);
    public bool IsEmpty => Width == 0 || Height == 0;

    internal static UiRectangle FromNative(NativeRectangle rectangle) =>
        new(rectangle.Left, rectangle.Top, rectangle.Right, rectangle.Bottom);
}

public sealed record UiCaptureResult(string NodePath, UiRectangle Bounds);

public sealed class UiLocateOptions
{
    public bool Flash { get; init; }
    public int StateBufferSize { get; init; } = WinUiAutomation.DefaultBufferSize;
}

public sealed record UiElementState(
    string Role,
    int Depth,
    IReadOnlyDictionary<string, string> Properties)
{
    public string? ControlType => GetValue("controlType");
    public string? AutomationId => GetValue("automationId");
    public string? ClassName => GetValue("className");
    public string? Name => GetValue("name");
    public string? Value => GetValue("value");
    public bool? IsEnabled => bool.TryParse(GetValue("enabled"), out bool value) ? value : null;
    public bool? IsOffscreen => GetValue("state") is string value ? value.Equals("Offscreen", StringComparison.OrdinalIgnoreCase) : null;

    public string? GetValue(string name) => Properties.TryGetValue(name, out string? value) ? value : null;
}

public sealed record UiLocateResult(
    bool Found,
    bool WasCanceled,
    UiRectangle Bounds,
    string RawState,
    IReadOnlyList<UiElementState> State,
    int NativeHResult)
{
    public UiElementState? TargetState => State.LastOrDefault(item => item.Role.Equals("Target", StringComparison.OrdinalIgnoreCase));
}

public sealed class UiAutomationException : ExternalException
{
    public UiAutomationException(string operation, int hresult)
        : base($"UI automation operation '{operation}' failed with HRESULT 0x{hresult:X8}.", hresult)
    {
        Operation = operation;
    }

    public string Operation { get; }
}

public sealed class WinUiAutomation : IDisposable
{
    public const int DefaultBufferSize = 1024 * 100;

    private const int EFail = unchecked((int)0x80004005);
    private const int EAbort = unchecked((int)0x80004004);
    private const int ErrorInsufficientBuffer = unchecked((int)0x8007007A);
    private const int SnapshotBufferRetryCount = 5;
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
    private static readonly SemaphoreSlim NativeGate = new(1, 1);
    private static int referenceCount;
    private bool disposed;

    public WinUiAutomation()
    {
        NativeGate.Wait();
        try
        {
            if (referenceCount == 0)
            {
                NativeMethods.Init();
            }

            referenceCount++;
        }
        finally
        {
            NativeGate.Release();
        }
    }

    public UiCaptureResult CapturePath(int screenX, int screenY, PathDirection direction = PathDirection.LeafToRoot, int bufferSize = DefaultBufferSize)
    {
        ValidateBufferSize(bufferSize);
        return Invoke(() =>
        {
            var path = new StringBuilder(bufferSize);
            NativeRectangle bounds = default;
            int result = unchecked((int)NativeMethods.CaptureUiPath(screenX, screenY, direction == PathDirection.LeafToRoot, path, path.Capacity, ref bounds));
            ThrowIfFailed(result, nameof(CapturePath));
            return new UiCaptureResult(path.ToString(), UiRectangle.FromNative(bounds));
        });
    }

    public UiLocateResult Locate(string nodePath, UiLocateOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodePath);
        options ??= new UiLocateOptions();
        ValidateBufferSize(options.StateBufferSize);

        return Invoke(() =>
        {
            var state = new StringBuilder(options.StateBufferSize);
            NativeRectangle bounds = default;
            int result = unchecked((int)NativeMethods.LocateUiByPath(nodePath, options.Flash, ref bounds, state, state.Capacity));
            if (result < 0 && result != EFail && result != EAbort)
            {
                throw new UiAutomationException(nameof(Locate), result);
            }

            UiRectangle rectangle = UiRectangle.FromNative(bounds);
            string rawState = state.ToString();
            IReadOnlyList<UiElementState> records = UiElementStateParser.Parse(rawState);
            return new UiLocateResult(result >= 0 && !rectangle.IsEmpty, result == EAbort, rectangle, rawState, records, result);
        });
    }

    public UiSnapshot GetSnapshot(nint rootWindow = 0, UiSnapshotOptions? options = null)
    {
        options ??= new UiSnapshotOptions();
        NativeUiSnapshotOptions nativeOptions = ToNative(options);
        return Invoke(() => ReadSnapshot(
            (StringBuilder? output, int capacity, out int requiredCapacity) =>
                NativeMethods.GetUiSnapshot(rootWindow, ref nativeOptions, output, capacity, out requiredCapacity),
            nameof(GetSnapshot)));
    }

    public UiSnapshot QuerySnapshot(UiSnapshotQuery query, nint rootWindow = 0, UiSnapshotOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(query);
        options ??= new UiSnapshotOptions();
        NativeUiSnapshotOptions nativeOptions = ToNative(options);
        NativeUiSnapshotQuery nativeQuery = ToNative(query);
        return Invoke(() => ReadSnapshot(
            (StringBuilder? output, int capacity, out int requiredCapacity) =>
                NativeMethods.QueryUiSnapshot(rootWindow, ref nativeOptions, ref nativeQuery, output, capacity, out requiredCapacity),
            nameof(QuerySnapshot)));
    }

    public void AbortLocate()
    {
        ThrowIfDisposed();
        NativeMethods.AbortLocateUiByPath();
    }

    public void Flash(UiRectangle bounds, int red = 0, int green = 255, int blue = 0, int durationMilliseconds = 1000, int inflate = 5)
    {
        ValidateByte(red, nameof(red));
        ValidateByte(green, nameof(green));
        ValidateByte(blue, nameof(blue));
        ArgumentOutOfRangeException.ThrowIfNegative(durationMilliseconds);
        ArgumentOutOfRangeException.ThrowIfNegative(inflate);

        Invoke(() =>
        {
            NativeRectangle nativeBounds = NativeRectangle.FromManaged(bounds);
            int result = unchecked((int)NativeMethods.FlashRect(ref nativeBounds, red, green, blue, durationMilliseconds, inflate));
            ThrowIfFailed(result, nameof(Flash));
            return 0;
        });
    }

    public void PerformMouseAction(UiAction action, UiRectangle bounds, int offsetX = 0, int offsetY = 0, int deltaX = 0, int deltaY = 0)
    {
        if(action == UiAction.DefaultInvoke)
        {
            InvokeElementAction(bounds.Left + offsetX, bounds.Top + offsetY);
            return;
        }

        Invoke(() =>
        {
            NativeMethods.MouseTask(ToNativeName(action), NativeRectangle.FromManaged(bounds), offsetX, offsetY, deltaX, deltaY);
            return 0;
        });
    }

    public void InvokeElementAction(int screenX, int screenY)
    {
        Invoke(() =>
        {
            int result = unchecked((int)NativeMethods.InvokeElementAction(screenX, screenY));
            ThrowIfFailed(result, nameof(InvokeElementAction));
            return 0;
        });
    }

    public void SendKeyboardInput(string text, bool capsLock = false, bool numLock = false, bool scrollLock = false)
    {
        ArgumentNullException.ThrowIfNull(text);
        Invoke(() =>
        {
            NativeMethods.KeyboardTask(text, capsLock, numLock, scrollLock);
            return 0;
        });
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        NativeGate.Wait();
        try
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            if (--referenceCount == 0)
            {
                NativeMethods.UnInit();
            }
        }
        finally
        {
            NativeGate.Release();
        }

        GC.SuppressFinalize(this);
    }

    private T Invoke<T>(Func<T> operation)
    {
        ThrowIfDisposed();
        NativeGate.Wait();
        try
        {
            ThrowIfDisposed();
            return operation();
        }
        finally
        {
            NativeGate.Release();
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(disposed, this);

    private static void ThrowIfFailed(int result, string operation)
    {
        if (result < 0)
        {
            throw new UiAutomationException(operation, result);
        }
    }

    private static void ValidateBufferSize(int bufferSize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(bufferSize, 2);
    }

    private static void ValidateByte(int value, string parameterName)
    {
        if (value is < 0 or > 255)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Color components must be between 0 and 255.");
        }
    }

    private static NativeUiSnapshotOptions ToNative(UiSnapshotOptions options)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaxDepth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaxElements);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaxVisitedElements);

        return new NativeUiSnapshotOptions
        {
            StructSize = checked((uint)Marshal.SizeOf<NativeUiSnapshotOptions>()),
            MaxDepth = checked((uint)options.MaxDepth),
            MaxElements = checked((uint)options.MaxElements),
            MaxVisitedElements = checked((uint)options.MaxVisitedElements),
            Flags = (uint)options.Flags
        };
    }

    private static NativeUiSnapshotQuery ToNative(UiSnapshotQuery query)
    {
        if (query.ProcessId is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(query), query.ProcessId, "ProcessId must be positive when specified.");
        }

        return new NativeUiSnapshotQuery
        {
            StructSize = checked((uint)Marshal.SizeOf<NativeUiSnapshotQuery>()),
            ControlType = NormalizeQueryValue(query.ControlType),
            Name = NormalizeQueryValue(query.Name),
            AutomationId = NormalizeQueryValue(query.AutomationId),
            ClassName = NormalizeQueryValue(query.ClassName),
            ProcessId = checked((uint)(query.ProcessId ?? 0)),
            Flags = (uint)query.Flags
        };
    }

    private static string? NormalizeQueryValue(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static UiSnapshot ReadSnapshot(SnapshotNativeCall nativeCall, string operation)
    {
        int result = unchecked((int)nativeCall(null, 0, out int requiredCapacity));
        ThrowIfFailed(result, operation);

        for (int attempt = 0; attempt < SnapshotBufferRetryCount; attempt++)
        {
            ValidateBufferSize(requiredCapacity);
            var output = new StringBuilder(requiredCapacity);
            result = unchecked((int)nativeCall(output, output.Capacity, out int nextRequiredCapacity));
            if (result >= 0)
            {
                return JsonSerializer.Deserialize<UiSnapshot>(output.ToString(), SnapshotJsonOptions)
                    ?? throw new JsonException($"UI automation operation '{operation}' returned an empty snapshot.");
            }

            if (result != ErrorInsufficientBuffer)
            {
                throw new UiAutomationException(operation, result);
            }

            requiredCapacity = nextRequiredCapacity;
        }

        throw new UiAutomationException(operation, ErrorInsufficientBuffer);
    }

    private static string ToNativeName(UiAction action) => action switch
    {
        UiAction.LeftClick => "LeftClick",
        UiAction.RightClick => "RightClick",
        UiAction.LeftDoubleClick => "LeftDblClick",
        UiAction.Drag => "Drag",
        UiAction.MouseWheel => "MouseWheel",
        UiAction.MouseHover => "MouseHover",
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
    };
}

internal delegate uint SnapshotNativeCall(StringBuilder? output, int capacity, out int requiredCapacity);

internal static partial class UiElementStateParser
{
    private static readonly Regex HeaderPattern = new(@"^(?<role>Root|Ancestor|Target)\s+depth=(?<depth>\d+)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex PropertyPattern = new(@"(?:^|,)(?<key>controlType|automationId|className|name|localizedControlType|helpText|itemStatus|accessKey|acceleratorKey|ariaRole|ariaProperties|value|legacyValue|enabled|state|expandCollapseState|rect)(?:=|:)", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static IReadOnlyList<UiElementState> Parse(string rawState)
    {
        if (string.IsNullOrWhiteSpace(rawState))
        {
            return Array.Empty<UiElementState>();
        }

        var records = new List<UiElementState>();
        foreach (string rawLine in rawState.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.Trim();
            Match header = HeaderPattern.Match(line);
            if (!header.Success || !int.TryParse(header.Groups["depth"].Value, out int depth))
            {
                continue;
            }

            MatchCollection matches = PropertyPattern.Matches(line, header.Length);
            var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < matches.Count; index++)
            {
                Match match = matches[index];
                int valueStart = match.Index + match.Length;
                int valueEnd = index + 1 < matches.Count ? matches[index + 1].Index : line.Length;
                properties[match.Groups["key"].Value] = line[valueStart..valueEnd].Trim().TrimEnd(',');
            }

            records.Add(new UiElementState(header.Groups["role"].Value, depth, new ReadOnlyDictionary<string, string>(properties)));
        }

        return records.AsReadOnly();
    }
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativeRectangle
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;

    public static NativeRectangle FromManaged(UiRectangle rectangle) => new()
    {
        Left = rectangle.Left,
        Top = rectangle.Top,
        Right = rectangle.Right,
        Bottom = rectangle.Bottom
    };
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativeUiSnapshotOptions
{
    public uint StructSize;
    public uint MaxDepth;
    public uint MaxElements;
    public uint MaxVisitedElements;
    public uint Flags;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct NativeUiSnapshotQuery
{
    public uint StructSize;
    [MarshalAs(UnmanagedType.LPWStr)]
    public string? ControlType;
    [MarshalAs(UnmanagedType.LPWStr)]
    public string? Name;
    [MarshalAs(UnmanagedType.LPWStr)]
    public string? AutomationId;
    [MarshalAs(UnmanagedType.LPWStr)]
    public string? ClassName;
    public uint ProcessId;
    public uint Flags;
}

internal static class NativeMethods
{
    private const string LibraryName = "UiAutoLib.dll";

    [DllImport(LibraryName, CallingConvention = CallingConvention.Winapi)]
    internal static extern void Init();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Winapi)]
    internal static extern void UnInit();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Winapi, CharSet = CharSet.Unicode)]
    internal static extern uint CaptureUiPath(int left, int top, [MarshalAs(UnmanagedType.Bool)] bool leafToRoot, StringBuilder path, int maxCount, ref NativeRectangle bounds);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Winapi, CharSet = CharSet.Unicode)]
    internal static extern uint LocateUiByPath(string nodePath, [MarshalAs(UnmanagedType.Bool)] bool flash, ref NativeRectangle bounds, StringBuilder state, int maxCount);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Winapi)]
    internal static extern uint AbortLocateUiByPath();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Winapi)]
    internal static extern uint FlashRect(ref NativeRectangle bounds, int red, int green, int blue, int duration, int inflate);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Winapi, CharSet = CharSet.Unicode)]
    internal static extern void MouseTask(string action, NativeRectangle bounds, int offsetX, int offsetY, int deltaX, int deltaY);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Winapi)]
    internal static extern uint InvokeElementAction(int left, int top);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Winapi, CharSet = CharSet.Unicode)]
    internal static extern void KeyboardTask(string text, [MarshalAs(UnmanagedType.Bool)] bool capsLock, [MarshalAs(UnmanagedType.Bool)] bool numLock, [MarshalAs(UnmanagedType.Bool)] bool scrollLock);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Winapi, CharSet = CharSet.Unicode)]
    internal static extern uint GetUiSnapshot(nint rootWindow, ref NativeUiSnapshotOptions options, StringBuilder? output, int outputCapacity, out int requiredCapacity);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Winapi, CharSet = CharSet.Unicode)]
    internal static extern uint QueryUiSnapshot(nint rootWindow, ref NativeUiSnapshotOptions options, ref NativeUiSnapshotQuery query, StringBuilder? output, int outputCapacity, out int requiredCapacity);
}
