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