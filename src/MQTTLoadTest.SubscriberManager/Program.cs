using MQTTLoadTest.Core.Models;
using MQTTLoadTest.Core.Services;
using MQTTLoadTest.SubscriberManager.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.CommandLine;
using Serilog;

namespace MQTTLoadTest.SubscriberManager;

class Program
{
    static async Task<int> Main(string[] args)
    {
        // Cấu hình Serilog
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .WriteTo.File("logs/subscriber-manager-.log", rollingInterval: RollingInterval.Day)
            .MinimumLevel.Information()
            .CreateLogger();

        try
        {
            Log.Information("Starting Subscriber Manager...");

            var host = CreateHostBuilder(args).Build();

            var rootCommand = new RootCommand("MQTT Load Test Subscriber Manager");

            rootCommand.AddCommand(CreateStartCommand(host));
            rootCommand.AddCommand(CreateStopCommand(host));
            rootCommand.AddCommand(CreateSubscribeCommand(host));
            rootCommand.AddCommand(CreateUnsubscribeCommand(host));
            rootCommand.AddCommand(CreateStatusCommand(host));
            rootCommand.AddCommand(CreateInteractiveCommand(host));

            if (args.Length == 0)
            {
                args = new[] { "interactive" };
            }

            return await rootCommand.InvokeAsync(args);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Subscriber Manager terminated unexpectedly");
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .UseSerilog() // Tích hợp Serilog vào host
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddJsonFile("../../../config/mqtt-config.json", optional: false, reloadOnChange: true);
                config.AddCommandLine(args);
            })
            .ConfigureServices((context, services) =>
            {
                services.Configure<MqttConfiguration>(context.Configuration);
                services.AddSingleton<IDeviceManager, DeviceManager>();
                services.AddSingleton<IPerformanceMonitor, PerformanceMonitor>();
                services.AddSingleton<IHighPerformanceSubscriber, HighPerformanceSubscriber>();
                services.AddSingleton<ISubscriptionManager, SubscriptionManagerService>();
                services.AddSingleton<SubscriberControlService>();
            });

    private static Command CreateStartCommand(IHost host)
    {
        var command = new Command("start", "Start subscriber");

        command.SetHandler(async () =>
        {
            var service = host.Services.GetRequiredService<SubscriberControlService>();
            await service.StartSubscriberAsync();
        });

        return command;
    }

    private static Command CreateStopCommand(IHost host)
    {
        var command = new Command("stop", "Stop subscriber");

        command.SetHandler(async () =>
        {
            var service = host.Services.GetRequiredService<SubscriberControlService>();
            await service.StopSubscriberAsync();
        });

        return command;
    }

    private static Command CreateSubscribeCommand(IHost host)
    {
        var deviceIdOption = new Option<string[]>("--device-ids", "Device IDs to subscribe to")
        {
            AllowMultipleArgumentsPerToken = true,
            IsRequired = true
        };

        var command = new Command("subscribe", "Subscribe to devices")
        {
            deviceIdOption
        };

        command.SetHandler(async (string[] deviceIds) =>
        {
            var service = host.Services.GetRequiredService<SubscriberControlService>();
            await service.SubscribeToDevicesAsync(deviceIds.ToList());
        }, deviceIdOption);

        return command;
    }

    private static Command CreateUnsubscribeCommand(IHost host)
    {
        var deviceIdOption = new Option<string[]>("--device-ids", "Device IDs to unsubscribe from")
        {
            AllowMultipleArgumentsPerToken = true,
            IsRequired = true
        };

        var command = new Command("unsubscribe", "Unsubscribe from devices")
        {
            deviceIdOption
        };

        command.SetHandler(async (string[] deviceIds) =>
        {
            var service = host.Services.GetRequiredService<SubscriberControlService>();
            await service.UnsubscribeFromDevicesAsync(deviceIds.ToList());
        }, deviceIdOption);

        return command;
    }

    private static Command CreateStatusCommand(IHost host)
    {
        var command = new Command("status", "Show subscription status");

        command.SetHandler(async () =>
        {
            var service = host.Services.GetRequiredService<SubscriberControlService>();
            await service.ShowStatusAsync();
        });

        return command;
    }

    private static Command CreateInteractiveCommand(IHost host)
    {
        var command = new Command("interactive", "Start interactive mode");

        command.SetHandler(async () =>
        {
            var service = host.Services.GetRequiredService<SubscriberControlService>();
            await service.RunInteractiveAsync();
        });

        return command;
    }
}
