using System.Collections.Generic;
using System.Threading.Tasks;
using Akka.Interfaced;

namespace Domain
{
    // Any user who is in a room
    [TagOverridable("senderUserId")]
    public interface IOccupant : IInterfacedActor
    {
        Task Say(string msg, string senderUserId = null);
        Task<IList<ChatItem>> GetHistory();
        Task Invite(string targetUserId, string senderUserId = null);
    }
}
