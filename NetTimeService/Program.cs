using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NetTimeService;

// Check for command-line administrative service actions
if (args.Length > 0)
{
    if (args[0].Equals("--install", StringComparison.OrdinalIgnoreCase))
    {
        string exePath = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "TrueTimeService.exe");
        bool ok = ServiceManager.InstallService(
            "TrueTimeService",
            "TrueTime Synchronization Service",
            "Authoritative multi-server internet time synchronization service with automated failover.",
            exePath);
        return ok ? 0 : 1;
    }

    if (args[0].Equals("--uninstall", StringComparison.OrdinalIgnoreCase))
    {
        bool ok = ServiceManager.UninstallService("TrueTimeService");
        return ok ? 0 : 1;
    }
}

var builder = Host.CreateApplicationBuilder(args);

// Configure as a Windows Service named TrueTimeService
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "TrueTimeService";
});

// Configure Windows Event Log provider
builder.Logging.AddEventLog(options =>
{
    options.SourceName = "TrueTimeService";
    options.LogName = "Application";
});

// Register services
builder.Services.AddSingleton<SntpClient>();
builder.Services.AddSingleton<ITimeSyncCoordinator, TimeSyncCoordinator>();

// Register background services
builder.Services.AddHostedService<Worker>();
builder.Services.AddHostedService<NamedPipeServer>();

var host = builder.Build();
host.Run();
return 0;
