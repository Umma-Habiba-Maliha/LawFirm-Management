using System;
using System.ComponentModel.DataAnnotations;

namespace LawFirmManagement.Models
{
    public class NotificationItem
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public string Title { get; set; } = "";

        public string Message { get; set; } = "";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsRead { get; set; } = false;

        // Target admin user id (IdentityUser Id) if specific, otherwise null for broadcast
        public string? ForUserId { get; set; }
    }
}
