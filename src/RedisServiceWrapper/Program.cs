using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using LanguageExt;
using static LanguageExt.Prelude;
using RedisServiceWrapper.Configuration;

namespace RedisServiceWrapper;

/// <summary>
/// Entry point for the Redis Windows Service Wrapper.
/// Implements functional programming principles with LanguageExt.
/// </summary>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            return await ProcessCommandLineArgs(args)
                .Match(
                    Some: async cmd => await ExecuteCommand(cmd),
                    None: async () => await RunAsService(args)
                );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fatal error: {ex.Message}");
            if (Debugger.IsAttached)
            {
                Console.Error.WriteLine(ex.StackTrace);
            }
            return 1;
        }
    }

    /// <summary>
    /// Processes command-line arguments using functional approach.
    /// Returns Some(Command) if a command is specified, None if running as service.
    /// </summary>
    private static Option<ServiceCommand> ProcessCommandLineArgs(string[] args) =>
        args.Length == 0
            ? None
            : args[0].ToLowerInvariant() switch
            {
                "--install" => Some(ServiceCommand.Install),
                "--uninstall" => Some(ServiceCommand.Uninstall),
                "--start" => Some(ServiceCommand.Start),
                "--stop" => Some(ServiceCommand.Stop),
                "--status" => Some(ServiceCommand.Status),
                "--help" or "-h" or "/?" => Some(ServiceCommand.Help),
                _ => None
            };

    /// <summary>
    /// Executes a command-line command.
    /// </summary>
    private static async Task<int> ExecuteCommand(ServiceCommand command) =>
        command switch
        {
            ServiceCommand.Install => ExecuteInstall(),
            ServiceCommand.Uninstall => ExecuteUninstall(),
            ServiceCommand.Start => ExecuteStart(),
            ServiceCommand.Stop => ExecuteStop(),
            ServiceCommand.Status => ExecuteStatus(),
            ServiceCommand.Help => ExecuteHelp(),
            _ => ExecuteHelp()
        };

    /// <summary>
    /// Runs the application as a Windows Service using functional composition.
    /// </summary>
    private static async Task<int> RunAsService(string[] args)
    {
        var host = CreateHostBuilder(args).Build();
        
        await host.RunAsync();
        
        return 0;
    }

    /// <summary>
    /// Creates the host builder with dependency injection and configuration.
    /// Follows functional programming principles with immutable configuration.
    /// </summary>
    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .UseWindowsService(options =>
            {
                options.ServiceName = Constants.ServiceName;
            })
            .ConfigureServices((hostContext, services) =>
            {
                // Register configuration (immutable)
                services.Configure<ServiceConfiguration>(
                    hostContext.Configuration.GetSection("ServiceConfiguration")
                );

                // Register hosted service
                services.AddHostedService<RedisService>();

                // Register logging (functional approach)
                services.AddSingleton<Logging.ILogger, Logging.CompositeLogger>();

                // Additional services will be registered in later tasks
            })
            .ConfigureLogging((hostContext, logging) =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.AddEventLog(settings =>
                {
                    settings.SourceName = Constants.EventLogSourceName;
                });
            });

    #region Command Implementations

    /// <summary>
    /// Installs the Windows Service.
    /// </summary>
    private static int ExecuteInstall()
    {
        Console.WriteLine("Installing Redis Windows Service...");
        Console.WriteLine("This functionality requires administrator privileges.");
        Console.WriteLine("Please use the PowerShell installation script or sc.exe");
        Console.WriteLine();
        Console.WriteLine("Example:");
        Console.WriteLine($"  sc.exe create {Constants.ServiceName} binPath=\"{GetExecutablePath()}\"");
        Console.WriteLine($"  sc.exe description {Constants.ServiceName} \"{Constants.ServiceDescription}\"");
        Console.WriteLine($"  sc.exe start {Constants.ServiceName}");
        return 0;
    }

    /// <summary>
    /// Uninstalls the Windows Service.
    /// </summary>
    private static int ExecuteUninstall()
    {
        Console.WriteLine("Uninstalling Redis Windows Service...");
        Console.WriteLine("This functionality requires administrator privileges.");
        Console.WriteLine();
        Console.WriteLine("Example:");
        Console.WriteLine($"  sc.exe stop {Constants.ServiceName}");
        Console.WriteLine($"  sc.exe delete {Constants.ServiceName}");
        return 0;
    }

    /// <summary>
    /// Starts the Windows Service.
    /// </summary>
    private static int ExecuteStart()
    {
        Console.WriteLine($"Starting {Constants.ServiceName}...");
        Try(() => System.ServiceProcess.ServiceController
            .GetServices()
            .FirstOrDefault(s => s.ServiceName == Constants.ServiceName))
            .Match(
                Succ: service =>
                {
                    if (service?.Status == System.ServiceProcess.ServiceControllerStatus.Stopped)
                    {
                        service.Start();
                        Console.WriteLine("Service started successfully.");
                        return 0;
                    }
                    Console.WriteLine($"Service is already {service?.Status}");
                    return 0;
                },
                Fail: ex =>
                {
                    Console.Error.WriteLine($"Error: {ex.Message}");
                    return 1;
                }
            );
        return 0;
    }

    /// <summary>
    /// Stops the Windows Service.
    /// </summary>
    private static int ExecuteStop()
    {
        Console.WriteLine($"Stopping {Constants.ServiceName}...");
        Try(() => System.ServiceProcess.ServiceController
            .GetServices()
            .FirstOrDefault(s => s.ServiceName == Constants.ServiceName))
            .Match(
                Succ: service =>
                {
                    if (service?.Status == System.ServiceProcess.ServiceControllerStatus.Running)
                    {
                        service.Stop();
                        Console.WriteLine("Service stopped successfully.");
                        return 0;
                    }
                    Console.WriteLine($"Service is already {service?.Status}");
                    return 0;
                },
                Fail: ex =>
                {
                    Console.Error.WriteLine($"Error: {ex.Message}");
                    return 1;
                }
            );
        return 0;
    }

    /// <summary>
    /// Shows the status of the Windows Service.
    /// </summary>
    private static int ExecuteStatus()
    {
        Try(() => System.ServiceProcess.ServiceController
            .GetServices()
            .FirstOrDefault(s => s.ServiceName == Constants.ServiceName))
            .Match(
                Succ: service =>
                {
                    if (service == null)
                    {
                        Console.WriteLine($"Service '{Constants.ServiceName}' is not installed.");
                        return 1;
                    }

                    Console.WriteLine($"Service Name: {service.ServiceName}");
                    Console.WriteLine($"Display Name: {service.DisplayName}");
                    Console.WriteLine($"Status: {service.Status}");
                    Console.WriteLine($"Start Type: {service.StartType}");
                    return 0;
                },
                Fail: ex =>
                {
                    Console.Error.WriteLine($"Error: {ex.Message}");
                    return 1;
                }
            );
        return 0;
    }

    /// <summary>
    /// Shows help information.
    /// </summary>
    private static int ExecuteHelp()
    {
        Console.WriteLine($"{Constants.ServiceDisplayName} v{Constants.Version}");
        Console.WriteLine(Constants.ServiceDescription);
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  RedisServiceWrapper.exe [command]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  --install       Show installation instructions");
        Console.WriteLine("  --uninstall     Show uninstallation instructions");
        Console.WriteLine("  --start         Start the service");
        Console.WriteLine("  --stop          Stop the service");
        Console.WriteLine("  --status        Show service status");
        Console.WriteLine("  --help, -h, /?  Show this help");
        Console.WriteLine();
        Console.WriteLine("When run without arguments, starts as a Windows Service.");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  RedisServiceWrapper.exe --status");
        Console.WriteLine("  RedisServiceWrapper.exe --help");
        return 0;
    }

    /// <summary>
    /// Gets the current executable path (pure function).
    /// </summary>
    private static string GetExecutablePath() =>
        Environment.ProcessPath ?? 
        System.Reflection.Assembly.GetExecutingAssembly().Location;

    #endregion
}

/// <summary>
/// Service commands (algebraic data type).
/// </summary>
public enum ServiceCommand
{
    Install,
    Uninstall,
    Start,
    Stop,
    Status,
    Help
}

