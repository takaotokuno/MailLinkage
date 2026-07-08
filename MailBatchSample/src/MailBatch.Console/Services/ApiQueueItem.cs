using MailBatch.Console.Models;
using MailKit;

namespace MailBatch.Console.Services;

internal sealed record ApiQueueItem(UniqueId Uid, ReceivedMailRequest Request);
