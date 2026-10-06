using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using ChatApi.DTOs;
using ChatApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ChatApi.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly IChatService _chatService;

    public ChatHub(IChatService chatService)
    {
        _chatService = chatService;
    }

    private int CurrentUserId =>
        int.Parse(Context.User!.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // Fires when a client connects (app opened / socket (re)established)
    public override async Task OnConnectedAsync()
    {
        // Flush unread messages that arrived while this user was offline
        var pending = await _chatService.GetUnreadMessagesAsync(CurrentUserId);

        foreach (var msg in pending)
        {
            await Clients.Caller.SendAsync("ReceiveMessage", msg);
        }

        await base.OnConnectedAsync();
    }

    // Client: connection.invoke("SendMessage", request)
    // request = { receiverId, type, content, mediaUrl, durationSeconds, latitude, longitude }
    public async Task SendMessage(SendMessageRequest request)
    {
        // SignalR does not run DataAnnotations / IValidatableObject automatically
        var results = new List<ValidationResult>();
        if (!Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true))
            throw new HubException(string.Join(" ", results.Select(r => r.ErrorMessage)));

        var senderId = CurrentUserId;

        ChatMessageResponse saved;
        try
        {
            saved = await _chatService.SaveMessageAsync(senderId, request);
        }
        catch (ArgumentException ex)
        {
            throw new HubException(ex.Message);
        }

        // Confirm to the sender (gives them the real Id/SentAt from the DB)
        await Clients.Caller.SendAsync("MessageSent", saved);

        // Deliver to the receiver if they're currently connected
        await Clients.User(request.ReceiverId.ToString())
            .SendAsync("ReceiveMessage", saved);

        // Lightweight notification (badge/toast), with a readable preview per type
        await Clients.User(request.ReceiverId.ToString())
            .SendAsync("ReceiveNotification", senderId, saved.Type, MessagePreview.Build(saved));
    }

    // Client: connection.invoke("MarkAsRead", senderId)
    public async Task MarkAsRead(int senderId)
    {
        var userId = CurrentUserId;
        await _chatService.MarkAsReadAsync(userId, senderId);

        await Clients.User(senderId.ToString())
            .SendAsync("MessagesRead", userId);
    }
}