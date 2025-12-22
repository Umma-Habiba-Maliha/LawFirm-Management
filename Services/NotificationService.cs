using Microsoft.AspNetCore.SignalR;
using LawFirmManagement.Data;
using LawFirmManagement.Hubs;
using LawFirmManagement.Models;
using System.Threading.Tasks;

namespace LawFirmManagement.Services
{
    public class NotificationService
    {
        private readonly ApplicationDbContext _db;
        private readonly IHubContext<AdminHub> _hub;

        public NotificationService(ApplicationDbContext db, IHubContext<AdminHub> hub)
        {
            _db = db;
            _hub = hub;
        }

        // 1. Notify Admins (Broadcast to "Admins" group)
        public async Task CreateForAdminsAsync(string title, string message)
        {
            var n = new NotificationItem
            {
                Title = title,
                Message = message,
                ForUserId = null, // Null means it's for all admins
                CreatedAt = System.DateTime.UtcNow
            };

            _db.Notifications.Add(n);
            await _db.SaveChangesAsync();

            // Push to SignalR Group "Admins"
            await _hub.Clients.Group("Admins").SendAsync("ReceiveNotification", new { Title = title, Message = message });
        }

        // 2. NEW: Notify Specific User (Targeted by User ID)
        public async Task NotifyUserAsync(string userId, string title, string message)
        {
            var n = new NotificationItem
            {
                Title = title,
                Message = message,
                ForUserId = userId, // Specific Target
                CreatedAt = System.DateTime.UtcNow
            };

            _db.Notifications.Add(n);
            await _db.SaveChangesAsync();

            // Push to Specific SignalR User
            await _hub.Clients.User(userId).SendAsync("ReceiveNotification", new { Title = title, Message = message });
        }
    }
}