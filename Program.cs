using Configurable_Cleanup;
using Microsoft.Extensions.Hosting.WindowsServices;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "Configurable Cleanup Service";
});

builder.Services.AddHostedService<Configurable_Cleanup.Configurable_Cleanup>();

var host = builder.Build();
host.Run();
