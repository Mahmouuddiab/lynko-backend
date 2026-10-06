using ChatApi.Models;

namespace ChatApi.DTOs;

public class ChatMessageResponse
{
    public int Id { get; set; }
    public int SenderId { get; set; }
    public int ReceiverId { get; set; }

    public MessageType Type { get; set; }
    public string? Content { get; set; }

    public string? MediaUrl { get; set; }
    public int? DurationSeconds { get; set; }

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public DateTime SentAt { get; set; }
    public bool IsRead { get; set; }
}