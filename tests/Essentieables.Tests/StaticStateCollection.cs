// Copyright (c) 2026 Cedric Boutin. Licensed under the MIT License. See LICENSE.txt in the project root.

using Xunit;

namespace Essentieables.Tests;

/// <summary>Tests that touch the MAUI static facades must not run in parallel with each other.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public class StaticStateCollection
{
    public const string Name = "MAUI static facades";
}
