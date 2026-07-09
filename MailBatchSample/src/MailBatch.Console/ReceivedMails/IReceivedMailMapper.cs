namespace MailBatch.Console.ReceivedMails;

internal interface IReceivedMailMapper
{
    ReceivedMailRequest ToRequest(ReceivedMailContent content);
}
