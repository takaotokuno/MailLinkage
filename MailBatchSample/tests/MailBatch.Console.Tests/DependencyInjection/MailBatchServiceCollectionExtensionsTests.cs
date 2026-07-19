using MailBatch.Console.DependencyInjection;
using MailBatch.Console.Options;
using MailBatch.Console.ReceivedMails.Processing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MailBatch.Console.Tests.DependencyInjection;

public sealed class MailBatchServiceCollectionExtensionsTests
{
    [Fact]
    public async Task AddBatchApplication_ReceivedMailRolesShareOneSessionInstanceWithinScope()
    {
        ServiceCollection services = new();
        _ = services.AddBatchApplication(new AppOptions(), "test-run", Serilog.Log.Logger);
        await using ServiceProvider provider = services.BuildServiceProvider();
        await using AsyncServiceScope scope = provider.CreateAsyncScope();

        IReceivedMailSession session = scope.ServiceProvider.GetRequiredService<IReceivedMailSession>();
        IReceivedMailSearcher searcher = scope.ServiceProvider.GetRequiredService<IReceivedMailSearcher>();
        IReceivedMailMover mover = scope.ServiceProvider.GetRequiredService<IReceivedMailMover>();

        Assert.Same(session, searcher);
        Assert.Same(session, mover);
    }
}
