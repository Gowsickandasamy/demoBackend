using DemoProject.Models;
using Microsoft.EntityFrameworkCore;

namespace DemoProject.Contexts
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }

        public DbSet<UserSession> UserSessions { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure the UserSession entity
            modelBuilder.Entity<UserSession>(entity =>
            {
                entity.HasKey(us => us.SessionId); // Primary key
                entity.Property(us => us.SessionId)
                      .HasDefaultValueSql("NEWID()"); // Auto-generate GUID for SessionId

                entity.HasOne(us => us.User) // Navigation property
                      .WithMany(u => u.UserSessions) // Users can have many sessions
                      .HasForeignKey(us => us.UserId) // Foreign key in UserSessions
                      .OnDelete(DeleteBehavior.Cascade); // Cascade delete
            });

            // Optional: Additional configurations for Users
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(u => u.Id); // Primary key
                entity.Property(u => u.Username)
                      .IsRequired()
                      .HasMaxLength(100);

                entity.Property(u => u.Password)
                      .IsRequired()
                      .HasMaxLength(100);
            });
        }
    }

}
