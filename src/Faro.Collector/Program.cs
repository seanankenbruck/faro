using FluentValidation;
using Faro.Collector.Services;
using Faro.Shared.Validation;

var builder = WebApplication.CreateBuilder(args);

// Add Faro services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add validation
builder.Services.AddValidatorsFromAssemblyContaining<MetricPointValidator>();

// Add Kafka producer
builder.Services.AddSingleton<IKafkaProducerService, KafkaProducerService>();

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