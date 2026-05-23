using Knowably.Contracts.Models;

namespace Knowably.Contracts.Interfaces;

public interface IQStashClient
{
    Task<string> PublishAsync(string destinationUrl, object body, QStashPublishOptions? options = null);
    Task<QStashMessage> GetMessageAsync(string messageId);
    Task<IEnumerable<QStashMessage>> ListMessagesAsync();
}
