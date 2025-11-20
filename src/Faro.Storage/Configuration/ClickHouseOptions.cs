namespace Faro.Storage.Configuration;

public class ClickHouseOptions
{
    public const string SectionName = "ClickHouse";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 8123;
    public string Database { get; set; } = "metrics";
    public string Username { get; set; } = "metrics_user";
    public string Password { get; set; } = "metrics_pass_123";
    public bool UseHttps { get; set; } = false;
    public int CommandTimeout { get; set; } = 30;
    public int MaxRetries { get; set; } = 3;

    public string ConnectionString =>
        $"Host={Host};Port={Port};Database={Database};Username={Username};Password={Password};Protocol={(UseHttps ? "https" : "http")};CommandTimeout={CommandTimeout}";
}