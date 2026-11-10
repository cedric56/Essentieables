using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Storage;
using NSubstitute;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Essentieables.Tests;

/// <summary>
/// The forwarding facades of Essentieables.Facades, shared by every multi-user host of a process (Blazor Server, Routeables.SignalR…):
/// each host registers how to find its running session, and the static facades forward to the first one that knows it.
/// </summary>
[Collection(StaticStateCollection.Name)]
public class EssentialsFacadesTests
{
    // Each test host keeps its "running session" on the flow, as Blazor Server and Routeables do.
    static readonly AsyncLocal<IServiceProvider?> s_hostA = new();
    static readonly AsyncLocal<IServiceProvider?> s_hostB = new();

    static ServiceProvider Session(IPreferences preferences, IShare? share = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(preferences);
        if (share is not null)
            services.AddSingleton(share);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void Every_contract_with_a_static_facade_gets_a_forwarding_facade()
    {
        EssentialsFacades.InstallForwarding();

        Assert.Equal(36, EssentialsFacades.Contracts.Count);
        foreach (var contract in EssentialsFacades.Contracts)
        {
            Assert.True(EssentialsFacades.TryGetInstalled(contract, out var facade));
            Assert.True(contract.IsAssignableFrom(facade));
        }
    }

    [Fact]
    public async Task Facades_forward_to_the_host_whose_session_is_running()
    {
        using var a = EssentialsFacades.AddSessionResolver("Test A", () => s_hostA.Value);
        using var b = EssentialsFacades.AddSessionResolver("Test B", () => s_hostB.Value);
        EssentialsFacades.InstallForwarding();
        var alice = new FakePreferences();
        var bob = new FakePreferences();
        using var aliceSession = Session(alice);
        using var bobSession = Session(bob);

        await Task.Run(() =>
        {
            s_hostA.Value = aliceSession;
            Preferences.Default.Set("user", "alice");
        });
        await Task.Run(() =>
        {
            s_hostB.Value = bobSession;
            Preferences.Default.Set("user", "bob");
            Assert.Same(bob, EssentialsFacades.Resolve<IPreferences>());
        });

        Assert.Equal("alice", alice.Get<string?>("user", null));
        Assert.Equal("bob", bob.Get<string?>("user", null));
    }

    [Fact]
    public async Task Facades_throw_outside_of_every_session_and_name_the_hosts()
    {
        using var a = EssentialsFacades.AddSessionResolver("Test A", () => s_hostA.Value);
        EssentialsFacades.InstallForwarding();

        await Task.Run(() =>
        {
            s_hostA.Value = null;
            var outside = Assert.Throws<InvalidOperationException>(() => Preferences.Default.ContainsKey("user"));
            Assert.Contains("outside of a session", outside.Message);
            Assert.Contains("Test A", outside.Message);
            Assert.Null(EssentialsFacades.CurrentServices);
        });
    }

    [Fact]
    public async Task A_disposed_resolver_is_no_longer_asked()
    {
        var registration = EssentialsFacades.AddSessionResolver("Test A", () => s_hostA.Value);
        EssentialsFacades.InstallForwarding();
        using var session = Session(new FakePreferences());

        await Task.Run(() =>
        {
            s_hostA.Value = session;
            Assert.Same(session, EssentialsFacades.CurrentServices);
            registration.Dispose();
            Assert.DoesNotContain("Test A", EssentialsFacades.Hosts);
            Assert.Throws<InvalidOperationException>(() => Preferences.Default.ContainsKey("user"));
        });
    }

    [Fact]
    public async Task Registering_a_host_again_replaces_its_resolver()
    {
        using var first = EssentialsFacades.AddSessionResolver("Test A", () => null);
        using var second = EssentialsFacades.AddSessionResolver("Test A", () => s_hostA.Value);
        using var session = Session(new FakePreferences());

        await Task.Run(() =>
        {
            s_hostA.Value = session;
            Assert.Same(session, EssentialsFacades.CurrentServices);
            Assert.Single(EssentialsFacades.Hosts, h => h == "Test A");
        });
    }

    [Fact]
    public void A_plain_install_never_replaces_an_extended_facade()
    {
        EssentialsFacades.Install<IAvaeShare, IShare>();
        EssentialsFacades.InstallForwarding();
        EssentialsFacades.Install<IShare, IShare>();

        Assert.IsAssignableFrom<IAvaeShare>(Share.Default);
        Assert.True(EssentialsFacades.TryGetInstalled(typeof(IShare), out var facade));
        Assert.Equal(typeof(IAvaeShare), facade);
    }

    [Fact]
    public void Two_unrelated_extended_facades_conflict()
    {
        EssentialsFacades.Install<IAvaeShare, IShare>();

        Assert.Throws<InvalidOperationException>(() => EssentialsFacades.Install<IOtherShare, IShare>());
    }

    [Fact]
    public void Only_essentials_contracts_have_a_facade()
    {
        Assert.Throws<ArgumentException>(() => EssentialsFacades.Install<IDisposable, IDisposable>());
    }

    [Fact]
    public async Task A_member_of_the_extended_facade_the_session_service_lacks_is_not_supported()
    {
        using var a = EssentialsFacades.AddSessionResolver("Test A", () => s_hostA.Value);
        EssentialsFacades.Install<IAvaeShare, IShare>();
        var share = Substitute.For<IShare>();
        using var session = Session(new FakePreferences(), share);

        await Task.Run(async () =>
        {
            s_hostA.Value = session;
            await Assert.ThrowsAsync<NotSupportedException>(() => ((IAvaeShare)Share.Default).RequestAsync("title", []));
        });
    }

    [Fact]
    public void Installing_again_restores_a_facade_that_was_replaced()
    {
        EssentialsFacades.InstallForwarding();
        var replaced = new FakePreferences();
        EssentialsAccessors.SetPreferences(null, replaced);
        Assert.Same(replaced, Preferences.Default);

        EssentialsFacades.InstallForwarding();

        Assert.NotSame(replaced, Preferences.Default);
    }

    public interface IOtherShare : IShare;

    sealed class FakePreferences : IPreferences
    {
        readonly Dictionary<string, object?> _values = [];

        public bool ContainsKey(string key, string? sharedName = null) => _values.ContainsKey(key);
        public void Remove(string key, string? sharedName = null) => _values.Remove(key);
        public void Clear(string? sharedName = null) => _values.Clear();
        public void Set<T>(string key, T value, string? sharedName = null) => _values[key] = value;
        public T Get<T>(string key, T defaultValue, string? sharedName = null)
            => _values.TryGetValue(key, out var value) ? (T)value! : defaultValue;
    }
}
