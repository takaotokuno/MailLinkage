using MailReceiver.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace MailReceiver.Api.Data;

public sealed class MailReceiverDbContext(DbContextOptions<MailReceiverDbContext> options) : DbContext(options)
{
    public DbSet<ReceivedMail> ReceivedMails
    {
        get
        {
            return Set<ReceivedMail>();
        }
    }

    /// <summary>
    /// 受信メールエンティティのテーブル、列、制約を構成します。
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<ReceivedMail> receivedMail = modelBuilder.Entity<ReceivedMail>();

        _ = receivedMail.ToTable("received_mails");
        _ = receivedMail.HasKey(mail => mail.Id);
        _ = receivedMail.Property(mail => mail.Id).HasColumnName("id");
        _ = receivedMail.Property(mail => mail.MessageId)
            .HasColumnName("message_id")
            .HasMaxLength(ReceivedMail.MESSAGE_ID_MAX_LENGTH)
            .IsRequired();
        _ = receivedMail.HasIndex(mail => mail.MessageId)
            .IsUnique()
            .HasDatabaseName("ux_received_mails_message_id");
        _ = receivedMail.Property(mail => mail.Sender)
            .HasColumnName("sender")
            .HasMaxLength(ReceivedMail.SENDER_MAX_LENGTH)
            .IsRequired();
        _ = receivedMail.Property(mail => mail.Subject)
            .HasColumnName("subject")
            .HasMaxLength(ReceivedMail.SUBJECT_MAX_LENGTH)
            .IsRequired();
        _ = receivedMail.Property(mail => mail.Body).HasColumnName("body");
        _ = receivedMail.Property(mail => mail.ReceivedAt)
            .HasColumnName("received_at")
            .IsRequired();
        _ = receivedMail.Property(mail => mail.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();
    }
}
