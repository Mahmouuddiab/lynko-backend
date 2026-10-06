using System.ComponentModel.DataAnnotations;
using ChatApi.Models;

namespace ChatApi.DTOs;

public class UploadMediaRequest
{
    [Required]
    public IFormFile File { get; set; } = null!;

    [Required]
    public MessageType Type { get; set; }
}