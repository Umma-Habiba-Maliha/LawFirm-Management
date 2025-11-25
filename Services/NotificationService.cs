using Microsoft.AspNetCore.SignalR;
using LawFirmManagement.Data;
using LawFirmManagement.Hubs;
using LawFirmManagement.Models;

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

        public async Task CreateForAdminsAsync(string title, string message)
        {
            // save broadcast notification
            var n = new NotificationItem { Title = title, Message = message, ForUserId = null };
            _db.Notifications.Add(n);
            await _db.SaveChangesAsync();

            // push
            await _hub.Clients.All.SendAsync("ReceiveNotification", new { Title = title, Message = message });
        }
    }
}
