using Microsoft.Graph.Models;

namespace EmailGraphAPI.Interface {
    public interface IEmailOperations {
        Task<List<Message>> LoadEmailsAsync();
        Task ProcessEmailPagesAsync(MessageCollectionResponse messages, List<Message> allMessages);
    }
}
