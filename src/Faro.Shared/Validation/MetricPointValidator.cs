using FluentValidation;
using Faro.Shared.Models;

namespace Faro.Shared.Validation;

public class MetricPointValidator: AbstractValidator<MetricPoint>
{
    public MetricPointValidator()
    {
        RuleFor(mp => mp.MetricName)
            .NotEmpty()
            .WithMessage("Metric name is required")
            .MaximumLength(200)
            .WithMessage("Metric name cannot exceed 200 characters")
            .Matches(@"^[a-zA-Z][a-zA-Z0-9._-]*$")
            .WithMessage("Metric name must start with a letter and can only contain letters, numbers, dots, underscores, and hyphens");

        RuleFor(mp => mp.Timestamp)
            .NotEmpty()
            .WithMessage("Timestamp is required")
            .Must(BeRecentTimestamp)
            .WithMessage("Timestamp must be within the last 1 hours");

        RuleFor(mp => mp.Value)
            .Must(BeFiniteNumber)
            .WithMessage("Metric value must be a finite number (not NaN or Infinity)");

        RuleFor(mp => mp.Tags)
            .NotNull()
            .WithMessage("Tags cannot be null")
            .Must(HaveValidTagCount)
            .WithMessage("Tags cannot exceed 20 key-value pairs");
    }

    private bool BeRecentTimestamp(DateTime timestamp)
    {
        var oneHourAgo = DateTime.UtcNow.AddHours(-1);
        return timestamp >= oneHourAgo && timestamp <= DateTime.UtcNow;
    }

    private bool BeFiniteNumber(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    private bool HaveValidTagCount(Dictionary<string, string> tags)
    {
        return tags.Count <= 20;
    }
}