using System.Collections.Generic;
using System.Threading.Tasks;
using MechanicalCataphract.Data.Entities;

namespace MechanicalCataphract.Services;

public interface IMessageService : IEntityService<Message>
{
    Task<IList<Message>> GetMessagesBySenderAsync(int commanderId);
    Task<IList<Message>> GetMessagesByTargetAsync(int commanderId);
    Task<IList<Message>> GetUndeliveredMessagesAsync();
    Task MarkAsDeliveredAsync(int messageId);
}
