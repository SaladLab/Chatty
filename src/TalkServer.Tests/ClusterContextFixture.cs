using System;
using Akka.Actor;
using Akka.Cluster.Utility;
using Akka.Interfaced;
using Domain;

namespace TalkServer
{
    public class ClusterContextFixture : IDisposable
    {
        public ClusterNodeContext Context { get; private set; }

        public ClusterContextFixture()
        {
            // force interface assembly to be loaded before creating ProtobufSerializer

            var type = typeof(IUser);
            if (type == null)
                throw new InvalidProgramException("!");
        }

        public void Initialize(ActorSystem system)
        {
            DeadRequestProcessingActor.Install(system);

            var context = new ClusterNodeContext { System = system };

            context.ClusterActorDiscovery = system.ActorOf(Props.Create(
                () => new ClusterActorDiscovery(null)));

            context.UserTable = new DistributedActorTableRef<string>(system.ActorOf(
                Props.Create(() => new DistributedActorTable<string>(
                    "User", context.ClusterActorDiscovery, null, null)),
                "UserTable"));

            context.UserTableContainer = new DistributedActorTableContainerRef<string>(system.ActorOf(
                Props.Create(() => new DistributedActorTableContainer<string>(
                    "User", context.ClusterActorDiscovery, typeof(UserActorFactory), new object[] { context }, InterfacedPoisonPill.Instance)),
                "UserTableContainer"));

            context.RoomTable = new DistributedActorTableRef<string>(system.ActorOf(
                Props.Create(() => new DistributedActorTable<string>(
                    "Room", context.ClusterActorDiscovery, null, null)),
                "RoomTable"));

            var roomTableContainer = system.ActorOf(
                Props.Create(() => new DistributedActorTableContainer<string>(
                    "Room", context.ClusterActorDiscovery, typeof(RoomActorFactory), new object[] { context }, InterfacedPoisonPill.Instance)),
                "RoomTableContainer");

            context.BotTable = new DistributedActorTableRef<long>(system.ActorOf(
                Props.Create(() => new DistributedActorTable<long>(
                    "Bot", context.ClusterActorDiscovery, typeof(IncrementalIntegerIdGenerator), null)),
                "BotTable"));

            var botTableContainer = system.ActorOf(
                Props.Create(() => new DistributedActorTableContainer<long>(
                    "Bot", context.ClusterActorDiscovery, typeof(BotActorFactory), new object[] { context }, InterfacedPoisonPill.Instance)),
                "BotTableContainer");

            Context = context;
        }

        public void Dispose()
        {
            if (Context == null)
                return;

            Context.System.Terminate();
            Context = null;
        }
    }
}
