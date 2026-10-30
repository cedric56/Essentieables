// Copyright (c) 2026 Cedric Boutin. Licensed under the MIT License. See LICENSE.txt in the project root.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Storage;
using NSubstitute;
using System;
using System.Linq.Expressions;
using Xunit;

namespace Essentieables.Tests;

[Collection(StaticStateCollection.Name)]
public class RegistrationTests
{
    static readonly System.Reflection.MethodInfo SetDefaults = typeof(Extensions).GetMethod(nameof(Extensions.SetDefaults))!;

    /// <summary>Calls SetDefaults with a substitute for every service, or null when <paramref name="withImplementations"/> is false.</summary>
    static object?[] BuildArguments(IServiceCollection services, bool withImplementations, out (Type Service, object? Implementation)[] implementations)
    {
        var parameters = SetDefaults.GetParameters();
        var args = new object?[parameters.Length];
        implementations = new (Type, object?)[parameters.Length];
        args[0] = services;

        for (var i = 1; i < parameters.Length; i++)
        {
            var type = parameters[i].ParameterType;
            if (type == typeof(ServiceLifetime))
            {
                args[i] = ServiceLifetime.Singleton;
                continue;
            }

            var serviceType = type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Func<>)
                ? type.GetGenericArguments()[0]
                : type;
            var implementation = withImplementations ? Substitute.For([serviceType], []) : null;
            implementations[i] = (serviceType, implementation);

            args[i] = serviceType == type
                ? implementation
                : Expression.Lambda(type, Expression.Constant(implementation, serviceType)).Compile();
        }

        return args;
    }

    [Fact]
    public void SetDefaults_installs_facades_and_registers_them_in_DI()
    {
        var services = new ServiceCollection();
        var args = BuildArguments(services, withImplementations: true, out var implementations);

        try
        {
            SetDefaults.Invoke(null, args);
            using var provider = services.BuildServiceProvider();

            foreach (var (serviceType, expected) in implementations)
            {
                if (expected is null)
                    continue;
                Assert.Same(expected, provider.GetRequiredService(serviceType));
            }

            Assert.Same(provider.GetRequiredService<IPreferences>(), Preferences.Default);
            Assert.Same(provider.GetRequiredService<IGeolocation>(), Geolocation.Default);
            Assert.Same(provider.GetRequiredService<IAppInfo>(), AppInfo.Current);
        }
        finally
        {
            SetDefaults.Invoke(null, BuildArguments(new ServiceCollection(), withImplementations: false, out _));
        }
    }

    [Fact]
    public void SetDefaults_without_implementations_does_not_throw()
    {
        // Regression: browser / fallback hosts used to pass () => VersionTracking.Default, which reads
        // AppInfo.Current.VersionString and throws when no IAppInfo is installed.
        var args = BuildArguments(new ServiceCollection(), withImplementations: false, out _);

        var exception = Record.Exception(() => SetDefaults.Invoke(null, args));

        Assert.Null(exception);
    }

    [Fact]
    public void RegisterAvaeEssentials_keeps_existing_registrations()
    {
        var mine = Substitute.For<IPreferences>();
        var services = new ServiceCollection();
        services.AddSingleton(mine);

        services.RegisterAvaeEssentials();

        using var provider = services.BuildServiceProvider();
        Assert.Same(mine, provider.GetRequiredService<IPreferences>());
    }
}
