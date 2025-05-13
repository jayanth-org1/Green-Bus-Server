using Microsoft.EntityFrameworkCore;
using TransportBooking.Models;

namespace TransportBooking.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Bookings> Bookings { get; set; }
        public DbSet<Models.Route> Routes { get; set; }
        public DbSet<Models.NotificationLog> NotificationLogs { get; set; }
        public DbSet<Models.Payment> Payments { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("users");
                entity.HasKey(e => e.id);
                entity.Property(e => e.username).IsRequired();
                entity.Property(e => e.email).IsRequired();
                entity.Property(e => e.created_at)
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");
            });
        }
    }
} 