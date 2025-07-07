using MQTTLoadTest.Core.Models;
using MQTTLoadTest.Core.Services;
using MQTTLoadTest.PublisherManager.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using System.CommandLine;
using Serilog;
using Microsoft.Extensions.Logging;

namespace MQTTLoadTest.PublisherManager;

class Program
{
    static async Task<int> Main(string[] args)
    {
        // Configure Serilog
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .WriteTo.File("logs/publisher-manager-.log", rollingInterval: RollingInterval.Day)
            .MinimumLevel.Information()
            .CreateLogger();

        try
        {
            Log.Information("Starting Publisher Manager...");

            var host = CreateHostBuilder(args).Build();

            var rootCommand = new RootCommand("MQTT Load Test Publisher Manager");

            // Commands
            rootCommand.AddCommand(CreateStartCommand(host));
            rootCommand.AddCommand(CreateStopCommand(host));
            rootCommand.AddCommand(CreateStatusCommand(host));
            rootCommand.AddCommand(CreateAddCommand(host));
            rootCommand.AddCommand(CreateRemoveCommand(host));
            rootCommand.AddCommand(CreateEnableCommand(host));
            rootCommand.AddCommand(CreateDisableCommand(host));
            rootCommand.AddCommand(CreateInteractiveCommand(host));

            if (args.Length == 0)
            {
                args = new[] { "interactive" };
            }

            return await rootCommand.InvokeAsync(args);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Host terminated unexpectedly");
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .UseSerilog() //Use Serilog instead of default logging
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddJsonFile("config/mqtt-config.json", optional: false, reloadOnChange: true);
                config.AddCommandLine(args);
            })
            .ConfigureServices((context, services) =>
            {
                services.Configure<MqttConfiguration>(context.Configuration.GetSection("MqttConfiguration"));
                services.AddSingleton<IDeviceManager, DeviceManager>();
                services.AddSingleton<IPerformanceMonitor, PerformanceMonitor>();
                services.AddSingleton<IPublisherManager, PublisherManagerService>();
                services.AddSingleton<PublisherControlService>();
                services.AddSingleton<ILoggerFactory, LoggerFactory>();
            });

    private static Command CreateStartCommand(IHost host)
    {
        var deviceIdOption = new Option<string[]>("--device-ids", "Specific device IDs to start") { AllowMultipleArgumentsPerToken = true };
        var allOption = new Option<bool>("--all", "Start all enabled publishers");

        var command = new Command("start", "Start publishers")
        {
            deviceIdOption,
            allOption
        };

        command.SetHandler(async (string[] deviceIds, bool all) =>
        {
            var service = host.Services.GetRequiredService<PublisherControlService>();
            await service.StartPublishersAsync(deviceIds, all);
        }, deviceIdOption, allOption);

        return command;
    }

    private static Command CreateStopCommand(IHost host)
    {
        var deviceIdOption = new Option<string[]>("--device-ids", "Specific device IDs to stop") { AllowMultipleArgumentsPerToken = true };
        var allOption = new Option<bool>("--all", "Stop all publishers");

        var command = new Command("stop", "Stop publishers")
        {
            deviceIdOption,
            allOption
        };

        command.SetHandler(async (string[] deviceIds, bool all) =>
        {
            var service = host.Services.GetRequiredService<PublisherControlService>();
            await service.StopPublishersAsync(deviceIds, all);
        }, deviceIdOption, allOption);

        return command;
    }

    private static Command CreateStatusCommand(IHost host)
    {
        var detailOption = new Option<bool>("--detail", "Show detailed status information");

        var command = new Command("status", "Show publisher status")
        {
            detailOption
        };

        command.SetHandler(async (bool detail) =>
        {
            var service = host.Services.GetRequiredService<PublisherControlService>();
            await service.ShowStatusAsync(detail);
        }, detailOption);

        return command;
    }

    private static Command CreateAddCommand(IHost host)
    {
        var countOption = new Option<int>("--count", () => 1, "Number of publishers to add");
        var deviceIdOption = new Option<string>("--device-id", "Specific device ID");
        var deviceNameOption = new Option<string>("--device-name", "Device name");

        var command = new Command("add", "Add new publishers")
        {
            countOption,
            deviceIdOption,
            deviceNameOption
        };

        command.SetHandler(async (int count, string deviceId, string deviceName) =>
        {
            var service = host.Services.GetRequiredService<PublisherControlService>();
            await service.AddPublishersAsync(count, deviceId, deviceName);
        }, countOption, deviceIdOption, deviceNameOption);

        return command;
    }

    private static Command CreateRemoveCommand(IHost host)
    {
        var deviceIdOption = new Option<string[]>("--device-ids", "Device IDs to remove")
        {
            AllowMultipleArgumentsPerToken = true,
            IsRequired = true
        };

        var command = new Command("remove", "Remove publishers")
        {
            deviceIdOption
        };

        command.SetHandler(async (string[] deviceIds) =>
        {
            var service = host.Services.GetRequiredService<PublisherControlService>();
            await service.RemovePublishersAsync(deviceIds.ToList());
        }, deviceIdOption);

        return command;
    }

    private static Command CreateEnableCommand(IHost host)
    {
        var deviceIdOption = new Option<string[]>("--device-ids", "Device IDs to enable")
        {
            AllowMultipleArgumentsPerToken = true,
            IsRequired = true
        };

        var command = new Command("enable", "Enable publishers")
        {
            deviceIdOption
        };

        command.SetHandler(async (string[] deviceIds) =>
        {
            var service = host.Services.GetRequiredService<PublisherControlService>();
            await service.EnablePublishersAsync(deviceIds.ToList());
        }, deviceIdOption);

        return command;
    }

    private static Command CreateDisableCommand(IHost host)
    {
        var deviceIdOption = new Option<string[]>("--device-ids", "Device IDs to disable")
        {
            AllowMultipleArgumentsPerToken = true,
            IsRequired = true
        };

        var command = new Command("disable", "Disable publishers")
        {
            deviceIdOption
        };

        command.SetHandler(async (string[] deviceIds) =>
        {
            var service = host.Services.GetRequiredService<PublisherControlService>();
            await service.DisablePublishersAsync(deviceIds.ToList());
        }, deviceIdOption);

        return command;
    }

    private static Command CreateInteractiveCommand(IHost host)
    {
        var command = new Command("interactive", "Start interactive mode");

        command.SetHandler(async () =>
        {
            var service = host.Services.GetRequiredService<PublisherControlService>();
            await service.RunInteractiveAsync();
        });

        return command;
    }
}
