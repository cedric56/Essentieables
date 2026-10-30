using Microsoft.Extensions.DependencyInjection;

namespace LibraryConsumer;

public static class Startup
{
    public static void Configure(IServiceCollection services) => Essentieables.AvaloniaExtensions.UseEssentieables(services);
}
