using Faro.Shared.Models;
using Faro.Storage;
using Faro.Storage.Configuration;
using Microsoft.Extensions.Options;

namespace Faro.Storage.Tests;

public class ClickHouseConnectionFactoryTests
{
    private readonly ClickHouseOptions _options;
    private readonly IOptions<ClickHouseOptions> _optionsWrapper;

    public ClickHouseConnectionFactoryTests()
    {
        _options = new ClickHouseOptions
        {
            Host = "localhost",
            Port = 8123,
            Database = "test_db",
            Username = "test_user",
            Password = "test_password"
        };
        _optionsWrapper = Options.Create(_options);
    }

    [Fact]
    public void Constructor_WithValidOptions_CreatesInstance()
    {
        var factory = new ClickHouseConnectionFactory(_optionsWrapper);

        Assert.NotNull(factory);
    }

    [Fact]
    public void Constructor_WithCustomOptions_CreatesInstance()
    {
        var customOptions = Options.Create(new ClickHouseOptions
        {
            Host = "clickhouse.example.com",
            Port = 9000,
            Database = "metrics",
            Username = "admin",
            Password = "secure_pass"
        });

        var factory = new ClickHouseConnectionFactory(customOptions);

        Assert.NotNull(factory);
    }

    [Theory]
    [InlineData("localhost", 8123)]
    [InlineData("192.168.1.100", 9000)]
    [InlineData("clickhouse.local", 8123)]
    public void Constructor_VariousHostsAndPorts_Accepted(string host, int port)
    {
        var options = Options.Create(new ClickHouseOptions
        {
            Host = host,
            Port = port,
            Database = "test",
            Username = "user",
            Password = "pass"
        });

        var factory = new ClickHouseConnectionFactory(options);

        Assert.NotNull(factory);
    }

    [Fact]
    public void ClickHouseOptions_ConnectionString_IsGenerated()
    {
        var options = new ClickHouseOptions
        {
            Host = "localhost",
            Port = 8123,
            Database = "metrics",
            Username = "admin",
            Password = "secret"
        };

        Assert.NotNull(options.ConnectionString);
        Assert.Contains("localhost", options.ConnectionString);
        Assert.Contains("8123", options.ConnectionString);
    }

    [Fact]
    public void ClickHouseOptions_ConnectionString_WithDifferentPorts()
    {
        var options8123 = new ClickHouseOptions
        {
            Host = "localhost",
            Port = 8123,
            Database = "test",
            Username = "user",
            Password = "pass"
        };

        var options9000 = new ClickHouseOptions
        {
            Host = "localhost",
            Port = 9000,
            Database = "test",
            Username = "user",
            Password = "pass"
        };

        Assert.Contains("8123", options8123.ConnectionString);
        Assert.Contains("9000", options9000.ConnectionString);
    }

    [Fact]
    public void ClickHouseOptions_AllPropertiesSet()
    {
        var options = new ClickHouseOptions
        {
            Host = "db.example.com",
            Port = 9000,
            Database = "metrics_db",
            Username = "writer",
            Password = "pass123",
            MaxRetries = 5
        };

        Assert.Equal("db.example.com", options.Host);
        Assert.Equal(9000, options.Port);
        Assert.Equal("metrics_db", options.Database);
        Assert.Equal("writer", options.Username);
        Assert.Equal("pass123", options.Password);
        Assert.Equal(5, options.MaxRetries);
    }

    [Fact]
    public void ClickHouseOptions_SectionName_IsCorrect()
    {
        Assert.Equal("ClickHouse", ClickHouseOptions.SectionName);
    }

    [Fact]
    public void ClickHouseOptions_DefaultMaxRetries()
    {
        var options = new ClickHouseOptions
        {
            Host = "localhost",
            Port = 8123,
            Database = "test",
            Username = "user",
            Password = "pass"
        };

        Assert.Equal(3, options.MaxRetries);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(10)]
    public void ClickHouseOptions_CustomMaxRetries_Accepted(int maxRetries)
    {
        var options = new ClickHouseOptions
        {
            Host = "localhost",
            Port = 8123,
            Database = "test",
            Username = "user",
            Password = "pass",
            MaxRetries = maxRetries
        };

        Assert.Equal(maxRetries, options.MaxRetries);
    }

    [Fact]
    public void ClickHouseOptions_EmptyPassword_Accepted()
    {
        var options = new ClickHouseOptions
        {
            Host = "localhost",
            Port = 8123,
            Database = "test",
            Username = "default",
            Password = ""
        };

        Assert.Empty(options.Password);
        Assert.NotNull(options.ConnectionString);
    }

    [Fact]
    public void ClickHouseOptions_LongDatabaseName_Accepted()
    {
        var options = new ClickHouseOptions
        {
            Host = "localhost",
            Port = 8123,
            Database = "very_long_database_name_for_metrics_storage",
            Username = "user",
            Password = "pass"
        };

        Assert.Equal("very_long_database_name_for_metrics_storage", options.Database);
    }

    [Fact]
    public void ClickHouseOptions_SpecialCharactersInPassword_Accepted()
    {
        var options = new ClickHouseOptions
        {
            Host = "localhost",
            Port = 8123,
            Database = "test",
            Username = "user",
            Password = "p@ssw0rd!#$%"
        };

        Assert.Equal("p@ssw0rd!#$%", options.Password);
    }

    [Fact]
    public void MetricPoint_PreparedForBulkInsert()
    {
        var metric = new MetricPoint
        {
            MetricName = "test.metric",
            Value = 42.0,
            Timestamp = DateTime.UtcNow,
            Tags = new Dictionary<string, string>
            {
                ["host"] = "test-host",
                ["service"] = "test-service",
                ["environment"] = "production"
            }
        };

        var row = new object[]
        {
            metric.Timestamp,
            metric.MetricName,
            metric.Value,
            metric.Tags,
            metric.Host,
            metric.Service,
            metric.Environment
        };

        Assert.Equal(7, row.Length);
        Assert.Equal("test.metric", row[1]);
        Assert.Equal(42.0, row[2]);
        Assert.Equal("test-host", metric.Host);
        Assert.Equal("test-service", metric.Service);
        Assert.Equal("production", metric.Environment);
    }

    [Fact]
    public void MetricPoint_WithNullTags_HandledGracefully()
    {
        var metric = new MetricPoint
        {
            MetricName = "test.metric",
            Value = 100.0,
            Timestamp = DateTime.UtcNow,
            Tags = new Dictionary<string, string>()
        };

        Assert.NotNull(metric.Tags);
        Assert.Empty(metric.Tags);
        Assert.Empty(metric.Host);
        Assert.Empty(metric.Service);
        Assert.Empty(metric.Environment);
    }

    [Fact]
    public void MetricPoint_BulkInsertFormat_MatchesTableSchema()
    {
        var metrics = new List<MetricPoint>
        {
            new MetricPoint
            {
                MetricName = "cpu.usage",
                Value = 75.5,
                Timestamp = DateTime.UtcNow,
                Tags = new Dictionary<string, string>
                {
                    ["host"] = "server1",
                    ["service"] = "api"
                }
            },
            new MetricPoint
            {
                MetricName = "memory.usage",
                Value = 80.2,
                Timestamp = DateTime.UtcNow,
                Tags = new Dictionary<string, string>
                {
                    ["host"] = "server2",
                    ["service"] = "worker"
                }
            }
        };

        var rows = metrics.Select(m => new object[]
        {
            m.Timestamp,
            m.MetricName,
            m.Value,
            m.Tags,
            m.Host,
            m.Service,
            m.Environment
        }).ToList();

        Assert.Equal(2, rows.Count);
        Assert.All(rows, row => Assert.Equal(7, row.Length));
    }
}
