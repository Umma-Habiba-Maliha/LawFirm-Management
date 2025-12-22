using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using LawFirmManagement.Data;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace LawFirmManagement.Controllers
{
    [Authorize] // Important: Only logged-in users can see this
    public class NotificationsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<IdentityUser> _userManager;

        public NotificationsController(ApplicationDbContext db, UserManager<IdentityUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        // 1. SHOW THE LIST
        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);
            var isAdmin = User.IsInRole("Admin");

            // Fetch my notifications
            var list = await _db.Notifications
                .Where(n => n.ForUserId == userId || (isAdmin && n.ForUserId == null))
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();

            return View(list);
        }

        // 2. MARK AS READ
        [HttpPost]
        public async Task<IActionResult> MarkRead(Guid id)
        {
            var n = await _db.Notifications.FindAsync(id);
            var userId = _userManager.GetUserId(User);
            var isAdmin = User.IsInRole("Admin");

            // Check permission before marking read
            if (n != null && (n.ForUserId == userId || (isAdmin && n.ForUserId == null)))
            {
                n.IsRead = true;
                await _db.SaveChangesAsync();
            }
            return RedirectToAction("Index");
        }
    }
}