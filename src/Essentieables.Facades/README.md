# Essentieables.Facades

The static facades of Microsoft.Maui.Essentials (`Preferences.Default`, `Geolocation.Default`, `Connectivity.Current`…), without any
host. This package has no `build/*.targets` and injects no sources: it is a plain library that
[Essentieables](../../README.md) and server hosts such as Routeables.SignalR depend on.

## Accessors

MAUI only exposes internal `SetDefault`/`SetCurrent` hooks. `EssentialsAccessors` reaches them with `[UnsafeAccessor]` (.NET 10), one
`SetX(null, implementation)` per contract (36), plus `CreateLocale` for the `Locale` constructor:

```csharp
EssentialsAccessors.SetPreferences(null, new MyPreferences());
```

## Forwarding facades for multi-user servers

A server that hosts many users in one process must never put a user's service into a process-wide facade. `EssentialsFacades` installs
facades that forward every member (methods, properties, events) to the service of the session that is running. Each host says how to
find it:

```csharp
EssentialsFacades.AddSessionResolver("Blazor Server", () => BlazorEssentials.CurrentSession?.Services);
EssentialsFacades.AddSessionResolver("Routeables", () => RouteableSession.Running?.Services);
EssentialsFacades.InstallForwarding();
```

- The resolvers are asked in order; the first that returns a service provider answers. Several hosts can share a process.
- Outside of every session, a facade throws `InvalidOperationException` naming the hosts: code started outside of a session (a hosted
  service, a timer created at startup) has no user to ask, so inject the service where a session runs and pass it along.
- `Install<TFacade, TService>()` installs a facade typed as an interface that extends the contract (Essentieables' `IAvaeShare` for
  `IShare`), so that helpers can still test `Share.Default` for it. A plain install never replaces an extended one, whatever the order. A
  member that the session's service does not implement throws `NotSupportedException`.
- Installing again sets the facades again (after `SetDefaults` or a test replaced one). `AddSessionResolver` returns an `IDisposable` that
  removes the resolver.
- The facades are `DispatchProxy` instances: for servers on the JIT, not for Native AOT or trimmed apps (`InstallForwarding` carries
  `RequiresDynamicCode`). The accessors themselves are AOT-compatible.
