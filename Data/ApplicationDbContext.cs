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
        public DbSet<Routes> Routes { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure relationships
            modelBuilder.Entity<Bookings>()
                .HasOne(b => b.User)
                .WithMany()
                .HasForeignKey(b => b.UserId);

            // Configure decimal precision for payment amount
            modelBuilder.Entity<Bookings>()
                .Property(b => b.PaymentAmount)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("users");
                entity.HasKey(e => e.id);
                entity.Property(e => e.username).IsRequired();
                entity.Property(e => e.email).IsRequired();
                entity.Property(e => e.created_at)
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");
            });
            
            // Configure Route entity
            modelBuilder.Entity<Routes>()
                .Property(r => r.BasePrice)
                .HasColumnType("decimal(18,2)");
        }
    }
} 