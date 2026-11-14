using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using Xunit;

namespace Essentieables.Tests;

/// <summary>
/// An Android, iOS, macOS or browser Avalonia app must get no Windows or Linux package: the Avalonia sources compile the Windows ports
/// and the GTK4 branch only where ESSENTIEABLES_WINDOWS / ESSENTIEABLES_LINUX are defined, and Essentieables.Avalonia only brings their
/// packages to the target frameworks that compile them.
/// </summary>
public class PlatformPackagesTests
{
    static readonly string[] DesktopPackages =
        ["WindowsContracts.Net.Foundation.UniversalApiContract", "Microsoft.Maui.Platforms.Linux.Gtk4.Essentials"];

    static string Root([CallerFilePath] string path = "") => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, "..", ".."));

    static Dictionary<string, string[]> Groups()
    {
        var nuspec = XDocument.Load(Path.Combine(Root(), "src", "Essentieables.Avalonia", "Essentieables.Avalonia.nuspec"));
        return nuspec.Descendants().Where(e => e.Name.LocalName == "group").ToDictionary(
            g => (string)g.Attribute("targetFramework")!,
            g => g.Elements().Select(d => (string)d.Attribute("id")!).ToArray());
    }

    [Fact]
    public void Only_the_desktop_group_brings_the_windows_contracts_and_gtk4()
    {
        var groups = Groups();

        Assert.All(DesktopPackages, p => Assert.Contains(p, groups["net10.0"]));
        foreach (var (framework, packages) in groups.Where(g => g.Key != "net10.0"))
            Assert.All(DesktopPackages, p => Assert.DoesNotContain(p, packages));
        Assert.Contains("Microsoft.Maui.Platforms.MacOS.Essentials", groups["net10.0-macos1.0"]);
        Assert.All(groups.Values, packages => Assert.Contains("Essentieables", packages));
    }

    [Fact]
    public void Only_the_browser_group_brings_JSInterop()
    {
        // Avalonia.Browser runs the browser implementations shared with Blazor, through AvaloniaBrowserJSRuntime.
        foreach (var (framework, packages) in Groups())
        {
            if (framework.StartsWith("net10.0-browser", StringComparison.Ordinal))
                Assert.Contains("Microsoft.JSInterop", packages);
            else
                Assert.DoesNotContain("Microsoft.JSInterop", packages);
        }
    }

    [Fact]
    public void The_browser_assets_ship_with_the_Blazor_sources()
    {
        var blazor = Path.Combine(Root(), "src", "Essentieables", "Blazor");
        foreach (var asset in new[] { "BlazorEssentials.js", "EssentieablesInterop.js", "dotnet-js-interop.js" })
            Assert.True(File.Exists(Path.Combine(blazor, asset)), asset);

        var targets = File.ReadAllText(Path.Combine(Root(), "src", "Essentieables", "build", "Essentieables.targets"));
        Assert.Contains("ESSENTIEABLES_AVALONIA_BROWSER", targets, StringComparison.Ordinal);
        Assert.Contains("EssentieablesInterop.js", targets, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("android")]
    [InlineData("ios")]
    [InlineData("macos")]
    [InlineData("maccatalyst")]
    [InlineData("browser")]
    public void Every_platform_has_its_own_group(string platform)
    {
        Assert.Single(Groups().Keys, k => k.StartsWith($"net10.0-{platform}", StringComparison.Ordinal));
    }

    [Fact]
    public void Windows_and_linux_code_of_the_avalonia_sources_is_guarded()
    {
        var sources = Path.Combine(Root(), "src", "Essentieables", "Avalonia");
        foreach (var file in Directory.GetFiles(sources, "*.cs"))
        {
            var lines = File.ReadAllLines(file);
            var guarded = new Stack<string>();
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (line.StartsWith("#if ", StringComparison.Ordinal))
                    guarded.Push(line[4..].Trim());
                else if (line.StartsWith("#endif", StringComparison.Ordinal))
                    guarded.Pop();
                else if (line.StartsWith("//", StringComparison.Ordinal))
                    continue;
                else if (Uses(line, "using Windows.", "Windows.Foundation", "Windows.Storage", "Windows.Devices", "Windows.System", "WinRT"))
                    Assert.True(guarded.Contains("ESSENTIEABLES_WINDOWS") || guarded.Contains("WINDOWS"),
                        $"{Path.GetFileName(file)}:{i + 1} uses Windows outside of ESSENTIEABLES_WINDOWS: {line}");
                else if (Uses(line, "Microsoft.Maui.Platforms.Linux"))
                    Assert.True(guarded.Contains("ESSENTIEABLES_LINUX"),
                        $"{Path.GetFileName(file)}:{i + 1} uses GTK4 outside of ESSENTIEABLES_LINUX: {line}");
            }
            Assert.Empty(guarded);
        }
    }

    static bool Uses(string line, params string[] names) => names.Any(n => line.Contains(n, StringComparison.Ordinal));
}
