using Akka.Cluster.Utility;
using Akka.Interfaced;

namespace TalkServer
{
    public class ClusterNodeContextUpdater : InterfacedActor
    {
        private readonly ClusterNodeContext _clusterContext;

        public ClusterNodeContextUpdater(ClusterNodeContext clusterContext)
        {
            _clusterContext = clusterContext;
        }

        protected override void PreStart()
        {
            _clusterContext.ClusterActorDiscovery.Tell(
                new ClusterActorDiscoveryMessage.MonitorActor("User"), Self);

            _clusterContext.ClusterActorDiscovery.Tell(
                new ClusterActorDiscoveryMessage.MonitorActor("Room"), Self);

            _clusterContext.ClusterActorDiscovery.Tell(
                new ClusterActorDiscoveryMessage.MonitorActor("Bot"), Self);
        }

        [MessageHandler]
        private void OnMessage(ClusterActorDiscoveryMessage.ActorUp m)
        {
            switch (m.Tag)
            {
                case "User":
                    _clusterContext.UserTable = new DistributedActorTableRef<string>(m.Actor);
                    break;

                case "Room":
                    _clusterContext.RoomTable = new DistributedActorTableRef<string>(m.Actor);
                    break;

                case "Bot":
                    _clusterContext.BotTable = new DistributedActorTableRef<long>(m.Actor);
                    break;
            }
        }

        [MessageHandler]
        private void OnMessage(ClusterActorDiscoveryMessage.ActorDown m)
        {
            switch (m.Tag)
            {
                case "User":
                    _clusterContext.UserTable = null;
                    break;

                case "Room":
                    _clusterContext.RoomTable = null;
                    break;

                case "Bot":
                    _clusterContext.BotTable = null;
                    break;
            }
        }
    }
}
