using ChatApi.DTOs;

namespace ChatApi.Services;

public interface IChatService
{
    Task<List<UserProfileResponse>> GetUsersAsync(int currentUserId);

    Task<ChatMessageResponse> SaveMessageAsync(int senderId, SendMessageRequest request);

    Task<List<ChatMessageResponse>> GetConversationAsync(int userId, int otherUserId);

    Task<List<ChatMessageResponse>> GetUnreadMessagesAsync(int userId);

    Task MarkAsReadAsync(int userId, int senderId);
}