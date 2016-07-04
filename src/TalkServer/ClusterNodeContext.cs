using Akka.Actor;
using Akka.Cluster.Utility;

namespace TalkServer
{
    public class ClusterNodeContext
    {
        public ActorSystem System;
        public IActorRef ClusterActorDiscovery;
        public IActorRef ClusterNodeContextUpdater;

        // quick access point for actors. but these are shared variables.
        // if there is a neat way to avoid this dirty hack, please improve it.
        public DistributedActorTableRef<string> UserTable;
        public DistributedActorTableContainerRef<string> UserTableContainer;
        public DistributedActorTableRef<string> RoomTable;
        public DistributedActorTableRef<long> BotTable;
    }
}
