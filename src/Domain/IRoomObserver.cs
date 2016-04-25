using System;
using Akka.Interfaced;

namespace Domain
{
    public interface IRoomObserver : IInterfacedObserver
    {
        void Enter(string userId);
        void Exit(string userId);
        void Say(ChatItem chatItem);
    }
}
