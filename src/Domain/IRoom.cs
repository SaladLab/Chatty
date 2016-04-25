using System.Threading.Tasks;
using Akka.Interfaced;

namespace Domain
{
    public interface IRoom : IInterfacedActor
    {
        Task<RoomInfo> Enter(string userId, IRoomObserver observer);
        Task Exit(string userId);
    }
}
