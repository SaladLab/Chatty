using System;
using Akka.Interfaced;

namespace Domain
{
    public interface IUserEventObserver : IInterfacedObserver
    {
        void Whisper(ChatItem chatItem);
        void Invite(string invitorUserId, string roomName);
    }
}
