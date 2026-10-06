using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using ChatApi.Data;
using ChatApi.DTOs;
using ChatApi.Models;
using Microsoft.EntityFrameworkCore;

namespace ChatApi.Services;

public class ChatService : IChatService
{
    private readonly AppDbContext _context;

    public ChatService(AppDbContext context)
    {
        _context = context;
    }

    // One projection used by every query, so a new field is added in one place only
    private static readonly Expression<Func<ChatMessage, ChatMessageResponse>> ToResponseExpr = m =>
        new ChatMessageResponse
        {
            Id = m.Id,
            SenderId = m.SenderId,
            ReceiverId = m.ReceiverId,
            Type = m.Type,
            Content = m.Content,
            MediaUrl = m.MediaUrl,
            DurationSeconds = m.DurationSeconds,
            Latitude = m.Latitude,
            Longitude = m.Longitude,
            SentAt = m.SentAt,
            IsRead = m.IsRead
        };

    private static readonly Func<ChatMessage, ChatMessageResponse> ToResponse = ToResponseExpr.Compile();

    public async Task<List<UserProfileResponse>> GetUsersAsync(int currentUserId)
    {
        return await _context.Users
            .AsNoTracking()
            .Where(u => u.Id != currentUserId)
            .OrderBy(u => u.FullName)
            .Select(u => new UserProfileResponse
            {
                Id = u.Id,
                FullName = u.FullName,
                Email = u.Email
            })
            .ToListAsync();
    }

    public async Task<ChatMessageResponse> SaveMessageAsync(int senderId, SendMessageRequest request)
    {
        if (senderId == request.ReceiverId)
            throw new ArgumentException("You cannot send a message to yourself.");

        var receiverExists = await _context.Users.AnyAsync(u => u.Id == request.ReceiverId);
        if (!receiverExists)
            throw new ArgumentException("Receiver not found.");

        var isMedia = request.Type is MessageType.Image or MessageType.Video or MessageType.Voice;
        if (isMedia)
            EnsureOwnUploadUrl(request.MediaUrl);

        var message = new ChatMessage
        {
            SenderId = senderId,
            ReceiverId = request.ReceiverId,
            Type = request.Type,
            Content = request.Content?.Trim(),
            MediaUrl = isMedia ? request.MediaUrl : null,
            DurationSeconds = request.Type is MessageType.Video or MessageType.Voice
                ? request.DurationSeconds
                : null,
            Latitude = request.Type == MessageType.Location ? request.Latitude : null,
            Longitude = request.Type == MessageType.Location ? request.Longitude : null,
            SentAt = DateTime.UtcNow,
            IsRead = false
        };

        _context.ChatMessages.Add(message);
        await _context.SaveChangesAsync();

        return ToResponse(message);
    }

    public async Task<List<ChatMessageResponse>> GetConversationAsync(int userId, int otherUserId)
    {
        return await _context.ChatMessages
            .AsNoTracking()
            .Where(m => (m.SenderId == userId && m.ReceiverId == otherUserId) ||
                        (m.SenderId == otherUserId && m.ReceiverId == userId))
            .OrderBy(m => m.SentAt)
            .Select(ToResponseExpr)
            .ToListAsync();
    }

    public async Task<List<ChatMessageResponse>> GetUnreadMessagesAsync(int userId)
    {
        return await _context.ChatMessages
            .AsNoTracking()
            .Where(m => m.ReceiverId == userId && !m.IsRead)
            .OrderBy(m => m.SentAt)
            .Select(ToResponseExpr)
            .ToListAsync();
    }

    public async Task MarkAsReadAsync(int userId, int senderId)
    {
        await _context.ChatMessages
            .Where(m => m.ReceiverId == userId && m.SenderId == senderId && !m.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.IsRead, true));
    }

    // Media must come from our own upload folder, never an arbitrary external link
    private static void EnsureOwnUploadUrl(string? mediaUrl)
    {
        if (!Uri.TryCreate(mediaUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || !uri.AbsolutePath.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase)
            || uri.AbsolutePath.Contains(".."))
        {
            throw new ArgumentException("Invalid media URL.");
        }
    }
}