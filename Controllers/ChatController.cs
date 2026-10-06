using System.Security.Claims;
using ChatApi.DTOs;
using ChatApi.Hubs;
using ChatApi.Models;
using ChatApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace ChatApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ChatController : ControllerBase
{
    private const long MaxUploadBytes = 100_000_000;

    private static readonly Dictionary<MessageType, (string[] Ext, long MaxBytes, string Folder)> UploadRules = new()
    {
        [MessageType.Image] = (new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" }, 10_000_000, "images"),
        [MessageType.Video] = (new[] { ".mp4", ".mov", ".webm" }, 100_000_000, "videos"),
        [MessageType.Voice] = (new[] { ".m4a", ".aac", ".mp3", ".ogg", ".wav" }, 20_000_000, "voice"),
    };

    private readonly IChatService _chatService;
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly IWebHostEnvironment _env;

    public ChatController(
        IChatService chatService,
        IHubContext<ChatHub> hubContext,
        IWebHostEnvironment env)
    {
        _chatService = chatService;
        _hubContext = hubContext;
        _env = env;
    }

    private int CurrentUserId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // GET: api/chat/users
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        var users = await _chatService.GetUsersAsync(CurrentUserId);
        return Ok(users);
    }

    // GET: api/chat/conversation/5
    [HttpGet("conversation/{otherUserId}")]
    public async Task<IActionResult> GetConversation(int otherUserId)
    {
        var messages = await _chatService.GetConversationAsync(CurrentUserId, otherUserId);
        return Ok(messages);
    }

    // POST: api/chat/mark-read/5
    // Marks every message that user 5 sent to the current user as read.
    // Kept separate from GET conversation on purpose: the chat list calls the
    // GET endpoint for every user to count unread messages.
    [HttpPost("mark-read/{otherUserId}")]
    public async Task<IActionResult> MarkRead(int otherUserId)
    {
        await _chatService.MarkAsReadAsync(CurrentUserId, otherUserId);
        return NoContent();
    }

    // POST: api/chat/upload   (multipart/form-data: file, type)
    // type = 1 Image, 2 Video, 3 Voice. Returns { url } to put in SendMessageRequest.MediaUrl
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxUploadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadBytes)]
    public async Task<IActionResult> Upload([FromForm] UploadMediaRequest request)
    {
        var file = request.File;
        var type = request.Type;

        if (!UploadRules.TryGetValue(type, out var rule))
            return BadRequest(new { error = "Type must be Image (1), Video (2) or Voice (3)." });

        if (file is null || file.Length == 0)
            return BadRequest(new { error = "Empty file." });

        if (file.Length > rule.MaxBytes)
            return BadRequest(new { error = $"File too large (max {rule.MaxBytes / 1_000_000} MB)." });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!rule.Ext.Contains(ext))
            return BadRequest(new { error = "File extension not allowed." });

        var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
        var dir = Path.Combine(webRoot, "uploads", rule.Folder);
        Directory.CreateDirectory(dir);

        // Never trust the client's file name
        var name = $"{Guid.NewGuid():N}{ext}";
        await using (var fs = System.IO.File.Create(Path.Combine(dir, name)))
        {
            await file.CopyToAsync(fs);
        }

        var url = $"{Request.Scheme}://{Request.Host}/uploads/{rule.Folder}/{name}";
        return Ok(new { url });
    }

    // POST: api/chat/send
    // Body: { receiverId, type, content, mediaUrl, durationSeconds, latitude, longitude }
    [HttpPost("send")]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest request)
    {
        var senderId = CurrentUserId;

        ChatMessageResponse saved;
        try
        {
            saved = await _chatService.SaveMessageAsync(senderId, request);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }

        // Push it live to the receiver if they have an open SignalR connection
        await _hubContext.Clients.User(request.ReceiverId.ToString())
            .SendAsync("ReceiveMessage", saved);

        await _hubContext.Clients.User(request.ReceiverId.ToString())
            .SendAsync("ReceiveNotification", senderId, saved.Type, MessagePreview.Build(saved));

        return Ok(saved);
    }
}