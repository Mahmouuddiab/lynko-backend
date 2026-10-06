using ChatApi.DTOs;
using ChatApi.Models;

namespace ChatApi.Services;

public static class MessagePreview
{
    public static string Build(ChatMessageResponse m) => m.Type switch
    {
        MessageType.Text => m.Content ?? string.Empty,
        MessageType.Image => "📷 Photo",
        MessageType.Video => "🎥 Video",
        MessageType.Voice => "🎤 Voice message",
        MessageType.Location => "📍 Location",
        _ => "New message"
    };
}