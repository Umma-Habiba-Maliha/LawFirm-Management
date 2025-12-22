using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using LawFirmManagement.Models;

namespace LawFirmManagement.Data
{
    public class ApplicationDbContext : IdentityDbContext<IdentityUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> opts) : base(opts) { }

        public DbSet<PendingUser> PendingUsers { get; set; }
        public DbSet<UserProfile> UserProfiles { get; set; }
        public DbSet<NotificationItem> Notifications { get; set; }
        public DbSet<Case> Cases { get; set; }
        public DbSet<Hearing> Hearings { get; set; }
        public DbSet<CaseDocument> CaseDocuments { get; set; }
        public DbSet<Payment> Payments { get; set; }


        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // -----------------------------------------------------
            // FIX MULTIPLE CASCADE PATHS FOR Case => AspNetUsers
            // -----------------------------------------------------

            builder.Entity<Case>()
                .HasOne(c => c.Client)
                .WithMany() // no navigation from IdentityUser
                .HasForeignKey(c => c.ClientId)
                .OnDelete(DeleteBehavior.Restrict);  // FIX

            builder.Entity<Case>()
                .HasOne(c => c.Lawyer)
                .WithMany() // no navigation from IdentityUser
                .HasForeignKey(c => c.LawyerId)
                .OnDelete(DeleteBehavior.Restrict);  // FIX
        }
    }
}
