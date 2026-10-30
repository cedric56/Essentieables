// Copyright (c) 2026 Cedric Boutin. Licensed under the MIT License. See LICENSE.txt in the project root.

using Microsoft.Extensions.DependencyInjection;

namespace LibraryConsumer;

public static class Startup
{
    public static void Configure(IServiceCollection services) => Essentieables.AvaloniaExtensions.UseEssentieables(services);
}
