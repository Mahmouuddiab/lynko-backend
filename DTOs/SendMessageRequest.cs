using System.ComponentModel.DataAnnotations;
using ChatApi.Models;

namespace ChatApi.DTOs;

public class SendMessageRequest : IValidatableObject
{
    [Range(1, int.MaxValue, ErrorMessage = "ReceiverId is required.")]
    public int ReceiverId { get; set; }

    public MessageType Type { get; set; } = MessageType.Text;

    [MaxLength(4000)]
    public string? Content { get; set; }

    [MaxLength(500)]
    public string? MediaUrl { get; set; }

    public int? DurationSeconds { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Enum.IsDefined(typeof(MessageType), Type))
        {
            yield return new ValidationResult("Invalid message type.", new[] { nameof(Type) });
            yield break;
        }

        switch (Type)
        {
            case MessageType.Text:
                if (string.IsNullOrWhiteSpace(Content))
                    yield return new ValidationResult(
                        "Content is required for text messages.", new[] { nameof(Content) });
                break;

            case MessageType.Image:
            case MessageType.Video:
            case MessageType.Voice:
                if (string.IsNullOrWhiteSpace(MediaUrl))
                    yield return new ValidationResult(
                        "MediaUrl is required for media messages.", new[] { nameof(MediaUrl) });

                if (DurationSeconds is < 0 or > 3600)
                    yield return new ValidationResult(
                        "DurationSeconds must be between 0 and 3600.", new[] { nameof(DurationSeconds) });
                break;

            case MessageType.Location:
                if (!IsValidCoordinate(Latitude, 90))
                    yield return new ValidationResult(
                        "Latitude must be between -90 and 90.", new[] { nameof(Latitude) });

                if (!IsValidCoordinate(Longitude, 180))
                    yield return new ValidationResult(
                        "Longitude must be between -180 and 180.", new[] { nameof(Longitude) });
                break;
        }
    }

    private static bool IsValidCoordinate(double? value, double limit) =>
        value.HasValue
        && !double.IsNaN(value.Value)
        && !double.IsInfinity(value.Value)
        && Math.Abs(value.Value) <= limit;
}