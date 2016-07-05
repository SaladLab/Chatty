using Aim.ClusterNode;
using Akka.Cluster.Utility;

namespace TalkServer
{
    public class ClusterNodeContext : ClusterNodeContextBase
    {
        [ClusterActor("User")]
        public DistributedActorTableRef<string> UserTable;

        public DistributedActorTableContainerRef<string> UserTableContainer;

        [ClusterActor("Room")]
        public DistributedActorTableRef<string> RoomTable;

        [ClusterActor("Bot")]
        public DistributedActorTableRef<long> BotTable;
    }
}
