namespace ChatApi.Models;

public class ChatMessage
{
    public int Id { get; set; }

    public int SenderId { get; set; }
    public int ReceiverId { get; set; }

    public MessageType Type { get; set; } = MessageType.Text;

    // Text body, or an optional caption for image/video
    public string? Content { get; set; }

    // Image / video / voice
    public string? MediaUrl { get; set; }
    public int? DurationSeconds { get; set; }   // video / voice

    // Location
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    public bool IsDelivered { get; set; }
    public bool IsRead { get; set; }

    // Navigation properties
    public User Sender { get; set; } = null!;
    public User Receiver { get; set; } = null!;
}