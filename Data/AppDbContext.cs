using ChatApi.Models;
using Microsoft.EntityFrameworkCore;

namespace ChatApi.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(
        DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.Email).IsUnique();

            // MySQL: an unbounded string becomes longtext, which can't be
            // indexed without a length. Cap it so the index can be created.
            entity.Property(u => u.Email).HasMaxLength(255);
        });

        modelBuilder.Entity<ChatMessage>(entity =>
        {
            // Both FKs point to User, so EF needs Restrict on at least one
            // to avoid MySQL's "multiple cascade paths" error.
            entity.HasOne(m => m.Sender)
                .WithMany()
                .HasForeignKey(m => m.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(m => m.Receiver)
                .WithMany()
                .HasForeignKey(m => m.ReceiverId)
                .OnDelete(DeleteBehavior.Restrict);

            // Speeds up conversation history queries
            entity.HasIndex(m => new { m.SenderId, m.ReceiverId });
            
            // Speeds up unread message queries
            entity.HasIndex(m => new { m.ReceiverId, m.IsRead });
        });
    }
}