using FluentValidation;
using Faro.Collector.Services;
using Faro.Shared.Validation;
using Faro.Storage;

var builder = WebApplication.CreateBuilder(args);

// Add Faro services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add validation
builder.Services.AddValidatorsFromAssemblyContaining<MetricPointValidator>();

// Add storage layer
builder.Services.AddMetricsStorage(builder.Configuration);

// Add metrics buffer as hosted service
builder.Services.AddHostedService<MetricsBuffer>();
builder.Services.AddSingleton<MetricsBuffer>(sp =>
    (MetricsBuffer)sp.GetServices<IHostedService>()
        .First(s => s is MetricsBuffer));

var app = builder.Build();

// Configure HTTP pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();
app.MapControllers();

app.Run();