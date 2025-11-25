using System.Collections.Generic;

namespace LawFirmManagement.Models
{
    public class AdminDashboardViewModel
    {
        public int TotalClients { get; set; }
        public int TotalLawyers { get; set; }
        public int PendingRequests { get; set; }
        public int UnreadNotifications { get; set; }

        public List<PendingUser> RecentPending { get; set; } = new();
        public List<NotificationItem> RecentNotifications { get; set; } = new();
    }
}
