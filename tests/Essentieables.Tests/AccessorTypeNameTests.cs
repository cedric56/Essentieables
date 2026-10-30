// Copyright (c) 2026 Cedric Boutin. Licensed under the MIT License. See LICENSE.txt in the project root.

using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;

namespace Essentieables.Tests;

/// <summary>
/// [UnsafeAccessorType] takes type names as strings, so a typo compiles and only fails at runtime
/// (it happened: "ConnectivtyImplementation", "AMicrosoft.Maui..."). These tests read the attributes
/// by reflection without invoking anything, so they also run on runners where the target assembly is absent.
/// </summary>
public class AccessorTypeNameTests
{
    const string MacOSAssembly = "Microsoft.Maui.Platforms.MacOS.Essentials";

    public static TheoryData<string> MacOSFactories()
    {
        var data = new TheoryData<string>();
        foreach (var m in typeof(MacosDefaults).GetMethods(BindingFlags.Public | BindingFlags.Static)
                     .Where(m => m.Name.StartsWith("Create", StringComparison.Ordinal)))
            data.Add(m.Name);
        return data;
    }

    [Theory]
    [MemberData(nameof(MacOSFactories))]
    public void MacOS_factory_points_to_the_matching_maui_labs_implementation(string methodName)
    {
        var method = typeof(MacosDefaults).GetMethod(methodName)!;
        var typeName = method.ReturnParameter.GetCustomAttribute<UnsafeAccessorTypeAttribute>()!.TypeName;

        var feature = methodName["Create".Length..];
        Assert.Equal($"{MacOSAssembly}.{feature}Implementation, {MacOSAssembly}", typeName);
    }

    public static TheoryData<string> MauiSetters()
    {
        var data = new TheoryData<string>();
        foreach (var m in typeof(EssentialsAccessors).GetMethods(BindingFlags.Public | BindingFlags.Static)
                     .Where(m => m.Name.StartsWith("Set", StringComparison.Ordinal)))
            data.Add(m.Name);
        return data;
    }

    [Theory]
    [MemberData(nameof(MauiSetters))]
    public void Maui_setter_targets_an_existing_facade_accepting_the_implementation(string methodName)
    {
        var method = typeof(EssentialsAccessors).GetMethod(methodName)!;
        var parameters = method.GetParameters();
        var facadeName = parameters[0].GetCustomAttribute<UnsafeAccessorTypeAttribute>()!.TypeName;

        var facade = Type.GetType(facadeName, throwOnError: false);
        Assert.NotNull(facade);

        var accessor = method.GetCustomAttribute<UnsafeAccessorAttribute>()!;
        var target = facade!.GetMethod(accessor.Name!, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public,
            [parameters[1].ParameterType]);
        Assert.NotNull(target);
    }
}
