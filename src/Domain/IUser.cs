using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Akka.Interfaced;

namespace Domain
{
    public interface IUser : IInterfacedActor
    {
        Task<string> GetId();
        Task<List<string>> GetRoomList();
        Task<Tuple<IOccupant, RoomInfo>> EnterRoom(string name, IRoomObserver observer);
        Task ExitFromRoom(string name);
        Task Whisper(string targetUserId, string message);
    }

    public interface IUserMessasing : IInterfacedActor
    {
        Task Whisper(ChatItem chatItem);
        Task Invite(string invitorUserId, string roomName);
    }
}
