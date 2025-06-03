using PrintHero.Service;
using Serilog;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PrintHero", "logs", "service-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30)
    .CreateLogger();

try
{
    var builder = Host.CreateApplicationBuilder(args);

    // Use Serilog
    builder.Services.AddSerilog();

    // Add the worker service
    builder.Services.AddHostedService<Worker>();

    var host = builder.Build();

    Log.Information("Starting PrintHero Service");
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "PrintHero Service terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}