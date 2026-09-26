using Microsoft.EntityFrameworkCore;
using SpendWise.Entities;

namespace SpendWise.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Chat> Chats => Set<Chat>();
    public DbSet<Spending> Spendings => Set<Spending>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Chat>(chat =>
        {
            chat.HasIndex(c => c.TelegramChatId).IsUnique();
            chat.HasIndex(c => c.ReportToken).IsUnique();
        });

        modelBuilder.Entity<Spending>(spending =>
        {
            spending.Property(s => s.Amount).HasPrecision(12, 2);
            spending.Property(s => s.Category).HasMaxLength(64);

            spending.HasOne(s => s.Chat)
                .WithMany(c => c.Spendings)
                .HasForeignKey(s => s.ChatId)
                .OnDelete(DeleteBehavior.Cascade);

            spending.HasIndex(s => new { s.ChatId, s.SpentAtUtc });
        });
    }
}
