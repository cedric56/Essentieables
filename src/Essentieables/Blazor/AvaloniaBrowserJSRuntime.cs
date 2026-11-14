// Avalonia.Browser only: compiled where ESSENTIEABLES_AVALONIA_BROWSER is defined (an Avalonia app built for the browser,
// see build/Essentieables.targets). Blazor hosts compile this file to nothing.
#if ESSENTIEABLES_AVALONIA_BROWSER

using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;
using System;
using System.IO;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Essentieables;

/// <summary>
/// The <see cref="IJSRuntime"/> of an Avalonia.Browser app. Blazor provides its own runtime; Avalonia.Browser has none,
/// so this one connects <see cref="JSRuntime"/> to the dispatcher of <c>@microsoft/dotnet-js-interop</c>
/// (<c>EssentieablesInterop.js</c>) with <c>[JSImport]</c>. The Blazor implementations of Essentieables then run unchanged
/// in Avalonia.Browser apps. It is registered in the app's services, so the app can inject <see cref="IJSRuntime"/> too.
/// </summary>
[SupportedOSPlatform("browser")]
internal sealed partial class AvaloniaBrowserJSRuntime : JSRuntime
{
    internal const string ModuleName = "essentieables-interop";
    internal const string ModuleFile = "EssentieablesInterop.js";

    // Chunk size used to read an IJSStreamReference (file picked, photo captured, app package file).
    const int StreamChunkSize = 512 * 1024;

    static AvaloniaBrowserJSRuntime? s_instance;
    static Task? s_attach;

    AvaloniaBrowserJSRuntime()
    {
    }

    public static AvaloniaBrowserJSRuntime Instance => s_instance ??= new AvaloniaBrowserJSRuntime();

    /// <summary>
    /// Imports <c>EssentieablesInterop.js</c> (next to <c>index.html</c>) and attaches the dispatcher. Idempotent; retried
    /// after a failure.
    /// </summary>
    internal static Task AttachAsync()
    {
        var attach = s_attach;
        if (attach is null || attach.IsFaulted || attach.IsCanceled)
            s_attach = attach = AttachCoreAsync();
        return attach;
    }

    static async Task AttachCoreAsync()
    {
        _ = Instance;
        await JSHost.ImportAsync(ModuleName, GetModuleUrl()).ConfigureAwait(false);
        Attach(OnMessage);
    }

    /// <summary>JSHost resolves relative URLs against the runtime scripts (<c>_framework/</c>), not the page: use an absolute URL.</summary>
    static string GetModuleUrl()
    {
        try
        {
            using var document = JSHost.GlobalThis.GetPropertyAsJSObject("document");
            var baseUri = document?.GetPropertyAsString("baseURI");
            if (!string.IsNullOrEmpty(baseUri) && Uri.TryCreate(baseUri, UriKind.Absolute, out var root))
                return new Uri(root, ModuleFile).ToString();
        }
        catch (System.Runtime.InteropServices.JavaScript.JSException)
        {
        }
        return "../" + ModuleFile;
    }

    // ───────────────────────── JS -> .NET ─────────────────────────

    static void OnMessage(string message)
    {
        try
        {
            using var document = JsonDocument.Parse(message);
            var root = document.RootElement;
            switch (root[0].GetString())
            {
                case "b":
                    {
                        // ["b", callId, assemblyName, methodIdentifier, dotNetObjectId, argsJson]
                        var callId = root[1].ValueKind == JsonValueKind.Null ? null : root[1].GetRawText().Trim('"');
                        var assemblyName = root[2].ValueKind == JsonValueKind.String ? root[2].GetString() : null;
                        var methodIdentifier = root[3].GetString() ?? string.Empty;
                        var dotNetObjectId = root[4].ValueKind == JsonValueKind.Number ? root[4].GetInt64() : 0;
                        var argsJson = root[5].GetString() ?? "[]";
                        var info = new DotNetInvocationInfo(assemblyName, methodIdentifier, dotNetObjectId, callId);
                        // Failures of the .NET method are reported to the JS promise by the dispatcher.
                        DotNetDispatcher.BeginInvokeDotNet(Instance, info, argsJson);
                        break;
                    }
                case "e":
                    // Exceptions of the awaited JS call are set on its task by the dispatcher.
                    DotNetDispatcher.EndInvokeJS(Instance, root[1].GetString() ?? "[]");
                    break;
                case "y":
                    DotNetDispatcher.ReceiveByteArray(Instance, root[1].GetInt32(), Convert.FromBase64String(root[2].GetString() ?? string.Empty));
                    break;
            }
        }
        catch (Exception ex)
        {
            // Never let an exception cross back into JS: it would surface as an unrelated JS error.
            Console.Error.WriteLine($"Essentieables: JS interop message failed: {ex}");
        }
    }

    // ───────────────────────── .NET -> JS ─────────────────────────

    protected override void BeginInvokeJS(long taskId, string identifier, string? argsJson, JSCallResultType resultType, long targetInstanceId)
        => BeginInvokeJSCore(taskId, identifier, argsJson ?? "[]", (int)resultType, targetInstanceId, (int)JSCallType.FunctionCall);

    protected override void BeginInvokeJS(in JSInvocationInfo invocationInfo)
        => BeginInvokeJSCore(
            invocationInfo.AsyncHandle,
            invocationInfo.Identifier,
            invocationInfo.ArgsJson ?? "[]",
            (int)invocationInfo.ResultType,
            invocationInfo.TargetInstanceId,
            (int)invocationInfo.CallType);

    protected override void EndInvokeDotNet(DotNetInvocationInfo invocationInfo, in DotNetInvocationResult invocationResult)
    {
        var resultOrError = invocationResult.Success
            ? invocationResult.ResultJson ?? "null"
            : invocationResult.Exception?.ToString() ?? "The .NET method failed.";
        EndInvokeDotNetCore(invocationInfo.CallId, invocationResult.Success, resultOrError);
    }

    protected override void SendByteArray(int id, byte[] data)
        => ReceiveByteArrayCore(id, data);

    protected override Task<Stream> ReadJSDataAsStreamAsync(IJSStreamReference jsStreamReference, long totalLength, CancellationToken cancellationToken = default)
        => Task.FromResult<Stream>(new JSDataStream(this, jsStreamReference, totalLength));

    protected override async Task TransmitStreamAsync(long streamId, DotNetStreamReference dotNetStreamReference)
    {
        try
        {
            using var buffer = new MemoryStream();
            await dotNetStreamReference.Stream.CopyToAsync(buffer).ConfigureAwait(false);
            SupplyDotNetStreamCore(streamId, buffer.ToArray(), null);
        }
        catch (Exception ex)
        {
            // The JS side waits for the stream: give it the error instead of leaving it pending.
            SupplyDotNetStreamCore(streamId, [], ex.Message);
        }
        finally
        {
            if (!dotNetStreamReference.LeaveOpen)
                dotNetStreamReference.Stream.Dispose();
        }
    }

    // ───────────────────────── [JSImport] ─────────────────────────

    [JSImport("attach", ModuleName)]
    private static partial void Attach([JSMarshalAs<JSType.Function<JSType.String>>] Action<string> onMessage);

    [JSImport("beginInvokeJS", ModuleName)]
    private static partial void BeginInvokeJSCore(
        [JSMarshalAs<JSType.Number>] long asyncHandle,
        string identifier,
        string argsJson,
        int resultType,
        [JSMarshalAs<JSType.Number>] long targetInstanceId,
        int callType);

    [JSImport("endInvokeDotNet", ModuleName)]
    private static partial void EndInvokeDotNetCore(string? callId, bool success, string resultOrError);

    [JSImport("receiveByteArray", ModuleName)]
    private static partial void ReceiveByteArrayCore(int id, [JSMarshalAs<JSType.Array<JSType.Number>>] byte[] data);

    [JSImport("supplyDotNetStream", ModuleName)]
    private static partial void SupplyDotNetStreamCore(
        [JSMarshalAs<JSType.Number>] long streamId,
        [JSMarshalAs<JSType.Array<JSType.Number>>] byte[] data,
        string? error);

    [JSImport("takeStagedChunk", ModuleName)]
    [return: JSMarshalAs<JSType.Array<JSType.Number>>]
    private static partial byte[] TakeStagedChunk(int token);

    /// <summary>
    /// The content of an <see cref="IJSStreamReference"/>, read by chunks: <c>essentieablesInterop.stageChunk</c> receives the
    /// Blob through the dispatcher (which revives the reference) and stages the bytes, taken synchronously with [JSImport].
    /// </summary>
    sealed class JSDataStream(AvaloniaBrowserJSRuntime runtime, IJSStreamReference reference, long length) : Stream
    {
        long _position;
        byte[] _chunk = [];
        int _chunkOffset;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => length;

        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (buffer.Length == 0)
                return 0;

            if (_chunkOffset >= _chunk.Length)
            {
                if (_position >= length)
                    return 0;

                var size = (int)Math.Min(StreamChunkSize, length - _position);
                var token = await runtime.InvokeAsync<int>(
                    "essentieablesInterop.stageChunk", cancellationToken, [reference, _position, size]).ConfigureAwait(false);
                _chunk = TakeStagedChunk(token);
                _chunkOffset = 0;
                if (_chunk.Length == 0)
                    return 0;
            }

            var count = Math.Min(buffer.Length, _chunk.Length - _chunkOffset);
            _chunk.AsSpan(_chunkOffset, count).CopyTo(buffer.Span);
            _chunkOffset += count;
            _position += count;
            return count;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        // The browser has a single thread: a synchronous read would deadlock waiting for JS.
        public override int Read(byte[] buffer, int offset, int count)
            => throw new NotSupportedException("Read the stream with ReadAsync or CopyToAsync in the browser.");

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

/// <summary>
/// JS helpers of the former Avae.Browser.Essentials (<c>Microsoft.Maui.Essentials.JSInterop</c>), for Avalonia.Browser apps.
/// They are synchronous ([JSImport]) and available once <c>BlazorEssentials.InitializeAsync()</c> has completed.
/// </summary>
[SupportedOSPlatform("browser")]
internal static partial class JSInterop
{
    /// <summary>Synchronous POST of <paramref name="data"/> (form-urlencoded) to <paramref name="url"/>; returns the response text.</summary>
    [JSImport("fetchSync", AvaloniaBrowserJSRuntime.ModuleName)]
    public static partial string Fetch(string url, string? data);

    /// <summary>Evaluates <paramref name="code"/> in the global scope.</summary>
    public static void Eval(string code) => Evaluate(code);

    /// <summary>Evaluates <paramref name="code"/> in the global scope and returns its result as a string (null when undefined).</summary>
    public static string? Query(string code) => Evaluate(code);

    /// <summary>Opens <paramref name="url"/> (window.open).</summary>
    public static void Open(string url, string target = "_blank") => OpenCore(url, target);

    [JSImport("open", AvaloniaBrowserJSRuntime.ModuleName)]
    private static partial void OpenCore(string url, string target);

    [JSImport("evaluate", AvaloniaBrowserJSRuntime.ModuleName)]
    private static partial string? Evaluate(string code);
}

/// <summary>Synchronous access to localStorage, usable before the JS module is imported.</summary>
[SupportedOSPlatform("browser")]
internal static partial class AvaloniaBrowserStorage
{
    [JSImport("globalThis.localStorage.key")]
    private static partial string? Key(int index);

    [JSImport("globalThis.localStorage.getItem")]
    internal static partial string? GetItem(string key);

    [JSImport("globalThis.localStorage.setItem")]
    private static partial void SetItem(string key, string value);

    [JSImport("globalThis.localStorage.removeItem")]
    private static partial void RemoveItem(string key);

    // Where the former browser implementation of Essentieables (Avalonia.Controls.Maui.Essentials) kept the preferences.
    const string LegacyPreferencesKey = "__avalonia_maui_preferences";
    const string LegacyPreferencesVersionKey = "__avalonia_maui_preferences_version";

    /// <summary>Loader of <see cref="BlazorPreferences"/>: moves the preferences of the former implementation once, then reads them.</summary>
    internal static System.Collections.Generic.Dictionary<string, string>? LoadPreferences(string prefix)
    {
        try
        {
            ImportLegacyPreferences();
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.JavaScript.JSException or JsonException or InvalidOperationException)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }
        return GetAll(prefix);
    }

    static void ImportLegacyPreferences()
    {
        var json = GetItem(LegacyPreferencesKey);
        if (string.IsNullOrEmpty(json))
            return;

        // { "sharedName": { "key": "value" } }, the values formatted with the invariant culture (DateTime as ToBinary()),
        // which BlazorPreferences reads. Keys already stored by BlazorPreferences win.
        using (var document = JsonDocument.Parse(json))
        {
            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var container in document.RootElement.EnumerateObject())
                {
                    if (container.Value.ValueKind != JsonValueKind.Object)
                        continue;
                    var sharedName = container.Name.Length == 0 ? null : container.Name;
                    foreach (var entry in container.Value.EnumerateObject())
                    {
                        if (entry.Value.ValueKind != JsonValueKind.String)
                            continue;
                        var key = BlazorPreferences.GetStorageKey(entry.Name, sharedName);
                        if (GetItem(key) is null)
                            SetItem(key, entry.Value.GetString()!);
                    }
                }
            }
        }

        RemoveItem(LegacyPreferencesKey);
        RemoveItem(LegacyPreferencesVersionKey);
    }

    static int Count()
    {
        using var storage = JSHost.GlobalThis.GetPropertyAsJSObject("localStorage");
        return storage?.GetPropertyAsInt32("length") ?? 0;
    }

    /// <summary>The entries whose key starts with <paramref name="prefix"/>; null when localStorage is not available.</summary>
    internal static System.Collections.Generic.Dictionary<string, string>? GetAll(string prefix)
    {
        try
        {
            var result = new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal);
            var count = Count();
            for (var i = 0; i < count; i++)
            {
                var key = Key(i);
                if (key is null || !key.StartsWith(prefix, StringComparison.Ordinal))
                    continue;
                var value = GetItem(key);
                if (value is not null)
                    result[key] = value;
            }
            return result;
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.JavaScript.JSException or InvalidOperationException)
        {
            // Storage disabled (privacy mode, sandboxed iframe): the asynchronous load reports it.
            System.Diagnostics.Debug.WriteLine(ex);
            return null;
        }
    }
}
#endif
