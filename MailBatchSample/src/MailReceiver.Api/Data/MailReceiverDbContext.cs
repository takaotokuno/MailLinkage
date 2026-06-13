using MailReceiver.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace MailReceiver.Api.Data;

public sealed class MailReceiverDbContext(DbContextOptions<MailReceiverDbContext> options) : DbContext(options)
{
    public DbSet<ReceivedMail> ReceivedMails => Set<ReceivedMail>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var receivedMail = modelBuilder.Entity<ReceivedMail>();

        receivedMail.ToTable("received_mails");
        receivedMail.HasKey(mail => mail.Id);
        receivedMail.Property(mail => mail.Id).HasColumnName("id");
        receivedMail.Property(mail => mail.MessageId)
            .HasColumnName("message_id")
            .HasMaxLength(255)
            .IsRequired();
        receivedMail.HasIndex(mail => mail.MessageId)
            .IsUnique()
            .HasDatabaseName("ux_received_mails_message_id");
        receivedMail.Property(mail => mail.Sender)
            .HasColumnName("sender")
            .HasMaxLength(320)
            .IsRequired();
        receivedMail.Property(mail => mail.Subject)
            .HasColumnName("subject")
            .HasMaxLength(500)
            .IsRequired();
        receivedMail.Property(mail => mail.Body).HasColumnName("body");
        receivedMail.Property(mail => mail.ReceivedAt)
            .HasColumnName("received_at")
            .IsRequired();
        receivedMail.Property(mail => mail.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();
    }
}
