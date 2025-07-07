public class LoggingConfiguration
{
    public string LogLevel { get; set; } = "Information";
    public string LogFilePath { get; set; } = "logs";
    public bool EnableConsoleLogging { get; set; } = true;
    public bool EnableFileLogging { get; set; } = true;
    public int MaxLogFileSizeMB { get; set; } = 50;
    public int MaxLogFiles { get; set; } = 10;
    public string LogFormat { get; set; } = "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}";
}