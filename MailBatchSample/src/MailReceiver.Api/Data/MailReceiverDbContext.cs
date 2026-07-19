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
        _ = receivedMail.Property(mail => mail.Key)
            .HasColumnName("key")
            .HasMaxLength(ReceivedMail.KEY_MAX_LENGTH)
            .IsRequired();
        _ = receivedMail.HasIndex(mail => mail.Key)
            .IsUnique()
            .HasDatabaseName("ux_received_mails_key");
        _ = receivedMail.Property(mail => mail.Message)
            .HasColumnName("message")
            .HasMaxLength(ReceivedMail.MESSAGE_MAX_LENGTH)
            .IsRequired();
        _ = receivedMail.Property(mail => mail.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();
    }
}
