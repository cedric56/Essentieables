using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using NSubstitute;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Essentieables.Tests;

/// <summary>
/// The test assembly is compiled with ESSENTIEABLES_BLAZOR_SERVER, as a Web SDK host is:
/// these tests cover the per-circuit isolation (audit SEC-01, SEC-02, TEST-01).
/// </summary>
[Collection(StaticStateCollection.Name)]
public class BlazorServerIsolationTests
{
    static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.UseBlazorEssentieables();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    [Fact]
    public void Server_build_is_detected()
    {
        Assert.True(BlazorEssentials.IsServer);
    }

    [Fact]
    public async Task Each_circuit_scope_gets_its_own_services_and_session()
    {
        await using var provider = BuildProvider();
        await using var a = provider.CreateAsyncScope();
        await using var b = provider.CreateAsyncScope();

        Assert.NotSame(a.ServiceProvider.GetRequiredService<BlazorEssentialsSession>(), b.ServiceProvider.GetRequiredService<BlazorEssentialsSession>());
        Assert.NotSame(a.ServiceProvider.GetRequiredService<IPreferences>(), b.ServiceProvider.GetRequiredService<IPreferences>());
        Assert.NotSame(a.ServiceProvider.GetRequiredService<ISecureStorage>(), b.ServiceProvider.GetRequiredService<ISecureStorage>());
        Assert.NotSame(a.ServiceProvider.GetRequiredService<IFileSystem>(), b.ServiceProvider.GetRequiredService<IFileSystem>());
        Assert.Same(a.ServiceProvider.GetRequiredService<IPreferences>(), a.ServiceProvider.GetRequiredService<IPreferences>());
        Assert.IsType<BlazorPreferences>(a.ServiceProvider.GetRequiredService<IPreferences>());
    }

    [Fact]
    public void Root_provider_cannot_hand_out_circuit_services()
    {
        using var provider = BuildProvider();

        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<IPreferences>());
    }

    [Fact]
    public async Task Static_facades_forward_to_the_current_circuit()
    {
        await using var provider = BuildProvider();
        await using var a = provider.CreateAsyncScope();
        await using var b = provider.CreateAsyncScope();
        var sessionA = a.ServiceProvider.GetRequiredService<BlazorEssentialsSession>();
        var sessionB = b.ServiceProvider.GetRequiredService<BlazorEssentialsSession>();

        var previous = BlazorEssentials.CurrentSession;
        try
        {
            BlazorEssentials.CurrentSession = sessionA;
            Preferences.Default.Set("user", "alice");
            Assert.Same(a.ServiceProvider.GetRequiredService<IPreferences>(), GetTarget<IPreferences>());

            BlazorEssentials.CurrentSession = sessionB;
            Assert.Null(Preferences.Default.Get<string?>("user", null));
            Preferences.Default.Set("user", "bob");

            BlazorEssentials.CurrentSession = sessionA;
            Assert.Equal("alice", Preferences.Default.Get<string?>("user", null));
        }
        finally
        {
            BlazorEssentials.CurrentSession = previous;
        }
    }

    static T GetTarget<T>() where T : class => BlazorEssentials.GetSessionService<T>();

    [Fact]
    public void Static_facades_throw_outside_of_a_circuit()
    {
        using var provider = BuildProvider();
        var previous = BlazorEssentials.CurrentSession;
        try
        {
            BlazorEssentials.CurrentSession = null;
            Assert.Throws<InvalidOperationException>(() => Preferences.Default.ContainsKey("user"));
            Assert.Throws<InvalidOperationException>(() => FileSystem.Current.CacheDirectory);
        }
        finally
        {
            BlazorEssentials.CurrentSession = previous;
        }
    }

    [Fact]
    public async Task Extended_facades_keep_the_avae_interfaces()
    {
        await using var provider = BuildProvider();
        Assert.IsAssignableFrom<IAvaeShare>(Microsoft.Maui.ApplicationModel.DataTransfer.Share.Default);
        Assert.IsAssignableFrom<IAvaeMediaPicker>(Microsoft.Maui.Media.MediaPicker.Default);
    }

    [Fact]
    public async Task Session_flows_into_work_started_by_the_circuit()
    {
        await using var provider = BuildProvider();
        await using var a = provider.CreateAsyncScope();
        var session = a.ServiceProvider.GetRequiredService<BlazorEssentialsSession>();

        var previous = BlazorEssentials.CurrentSession;
        try
        {
            BlazorEssentials.CurrentSession = session;
            var seen = await Task.Run(() => BlazorEssentials.CurrentSession);
            Assert.Same(session, seen);
        }
        finally
        {
            BlazorEssentials.CurrentSession = previous;
        }
    }

    [Fact]
    public async Task Circuit_handler_makes_the_session_current_for_inbound_activity_only()
    {
        await using var provider = BuildProvider();
        await using var a = provider.CreateAsyncScope();
        var session = a.ServiceProvider.GetRequiredService<BlazorEssentialsSession>();
        var handler = new BlazorEssentialsCircuitHandler(session);

        var previous = BlazorEssentials.CurrentSession;
        try
        {
            BlazorEssentials.CurrentSession = null;
            BlazorEssentialsSession? seen = null;
            var activity = handler.CreateInboundActivityHandler(_ =>
            {
                seen = BlazorEssentials.CurrentSession;
                return Task.CompletedTask;
            });

            await activity(null!);

            Assert.Same(session, seen);
            Assert.Null(BlazorEssentials.CurrentSession); // not leaked to the caller
        }
        finally
        {
            BlazorEssentials.CurrentSession = previous;
        }
    }

    [Fact]
    public async Task Server_directories_are_private_to_the_session_and_deleted_with_it()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var a = new BlazorEssentialsSession(services);
        var b = new BlazorEssentialsSession(services);

        var cacheA = a.GetDirectory("cache");
        var dataA = a.GetDirectory("appdata");
        var cacheB = b.GetDirectory("cache");

        Assert.NotEqual(cacheA, cacheB);
        Assert.StartsWith(Path.GetTempPath(), cacheA);
        Assert.DoesNotContain(cacheB, cacheA);
        Assert.True(Directory.Exists(dataA));

        await File.WriteAllTextAsync(Path.Combine(cacheA, "secret.txt"), "alice");
        await a.DisposeAsync();

        Assert.False(Directory.Exists(cacheA));
        Assert.True(Directory.Exists(cacheB));
        await b.DisposeAsync();
    }

    [Fact]
    public async Task InitializeAsync_imports_the_module_once_per_session()
    {
        var js = Substitute.For<IJSRuntime>();
        var module = Substitute.For<IJSObjectReference>();
        js.InvokeAsync<IJSObjectReference>("import", Arg.Any<object?[]?>())
          .Returns(new ValueTask<IJSObjectReference>(module));

        var services = new ServiceCollection();
        services.AddScoped(_ => js);
        services.UseBlazorEssentieables();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using var a = provider.CreateAsyncScope();
        await using var b = provider.CreateAsyncScope();

        var previous = BlazorEssentials.CurrentSession;
        try
        {
            await BlazorEssentials.InitializeAsync(a.ServiceProvider);
            await BlazorEssentials.InitializeAsync(a.ServiceProvider);

            Assert.Same(module, a.ServiceProvider.GetRequiredService<BlazorEssentialsSession>().Module);
            Assert.Null(b.ServiceProvider.GetRequiredService<BlazorEssentialsSession>().Module);
            await js.Received(1).InvokeAsync<IJSObjectReference>("import", Arg.Any<object?[]?>());
        }
        finally
        {
            BlazorEssentials.CurrentSession = previous;
        }
    }
}

public class BlazorPreferencesTests
{
    static BlazorEssentialsSession SessionWith(IJSObjectReference module) =>
        new(new ServiceCollection().BuildServiceProvider()) { Module = module };

    [Fact]
    public async Task Writes_made_before_the_load_are_not_overwritten_by_it()
    {
        var module = Substitute.For<IJSObjectReference>();
        module.InvokeAsync<Dictionary<string, string>>("prefsGetAll", Arg.Any<object?[]?>())
              .Returns(new ValueTask<Dictionary<string, string>>(new Dictionary<string, string>
              {
                  ["maui:prefs:_default:a"] = "stored",
                  ["maui:prefs:_default:b"] = "stored",
                  ["maui:prefs:_default:c"] = "stored",
              }));

        var previous = BlazorEssentials.CurrentSession;
        try
        {
            BlazorEssentials.CurrentSession = SessionWith(module);
            var preferences = new BlazorPreferences();

            preferences.Set("a", "mine");
            preferences.Remove("b");
            await preferences.InitializeAsync();
            await preferences.FlushAsync();

            Assert.True(preferences.IsLoaded);
            Assert.Equal("mine", preferences.Get<string?>("a", null));
            Assert.False(preferences.ContainsKey("b"));
            Assert.Equal("stored", preferences.Get<string?>("c", null));
        }
        finally
        {
            BlazorEssentials.CurrentSession = previous;
        }
    }

    [Fact]
    public async Task Persistence_failures_are_reported_by_FlushAsync()
    {
        var module = Substitute.For<IJSObjectReference>();
        module.InvokeAsync<IJSVoidResult>("prefsSet", Arg.Any<object?[]?>())
              .Returns<ValueTask<IJSVoidResult>>(_ => throw new JSException("quota exceeded"));

        var previous = BlazorEssentials.CurrentSession;
        try
        {
            BlazorEssentials.CurrentSession = SessionWith(module);
            var preferences = new BlazorPreferences();

            preferences.Set("a", 1);

            await Assert.ThrowsAsync<AggregateException>(preferences.FlushAsync);
        }
        finally
        {
            BlazorEssentials.CurrentSession = previous;
        }
    }

    [Theory]
    [InlineData(3.14159265358979)]
    [InlineData(-0.1)]
    [InlineData(1e-300)]
    public void Doubles_round_trip(double value)
    {
        using var _ = new CultureScope("fr-FR");
        var preferences = new BlazorPreferences();

        preferences.Set("d", value);

        Assert.Equal(value, preferences.Get("d", 0d));
    }

    [Fact]
    public void Unsupported_types_throw_like_MAUI()
    {
        var preferences = new BlazorPreferences();

        Assert.Throws<NotSupportedException>(() => preferences.Set("g", Guid.NewGuid()));
        Assert.Throws<NotSupportedException>(() => preferences.Get("g", Guid.Empty));
    }
}

public class BlazorSecureStorageTests
{
    [Fact]
    public async Task Read_failures_are_not_reported_as_an_empty_value()
    {
        var module = Substitute.For<IJSObjectReference>();
        module.InvokeAsync<string?>("secureGet", Arg.Any<object?[]?>())
              .Returns<ValueTask<string?>>(_ => throw new JSException("IndexedDB unavailable"));

        var previous = BlazorEssentials.CurrentSession;
        try
        {
            BlazorEssentials.CurrentSession = new BlazorEssentialsSession(new ServiceCollection().BuildServiceProvider()) { Module = module };
            var storage = new BlazorSecureStorage();

            await Assert.ThrowsAsync<JSException>(() => storage.GetAsync("token"));
        }
        finally
        {
            BlazorEssentials.CurrentSession = previous;
        }
    }
}
