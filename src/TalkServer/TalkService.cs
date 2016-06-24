using System;
using System.Threading;
using System.Threading.Tasks;
using Akka.Configuration;
using Domain;

namespace TalkServer
{
    public class TalkService
    {
        public async Task RunAsync(string[] args, CancellationToken cancellationToken)
        {
            // force interface assembly to be loaded before creating ProtobufSerializer

            var type = typeof(IUser);
            if (type == null)
                throw new InvalidProgramException("!");

            // connect to redis

            try
            {
                RedisStorage.Instance = new RedisStorage("localhost");
            }
            catch (Exception e)
            {
                Console.WriteLine("Error in connecting redis server: " + e);
                return;
            }

            // run cluster nodes

            var clusterRunner = CreateClusterRunner();

            var standAlone = args.Length > 0 && args[0] == "standalone";
            if (standAlone)
            {
                await clusterRunner.LaunchNode(3001, 9001, "room-table", "user-table", "room", "user", "bot");
            }
            else
            {
                await clusterRunner.LaunchNode(3001, 0, "room-table");
                await clusterRunner.LaunchNode(3002, 0, "user-table");
                await clusterRunner.LaunchNode(3011, 0, "room");
                await clusterRunner.LaunchNode(3012, 0, "room");
                await clusterRunner.LaunchNode(3021, 9001, "user");
                await clusterRunner.LaunchNode(3022, 9002, "user");
                await clusterRunner.LaunchNode(3031, 0, "bot-user");
            }

            try
            {
                await Task.Delay(-1, cancellationToken);
            }
            catch (TaskCanceledException)
            {
                // ignore cancellation exception
            }

            await clusterRunner.Shutdown();
        }

        private ClusterRunner CreateClusterRunner()
        {
            var commonConfig = ConfigurationFactory.ParseString(@"
                akka {
                  actor {
                    provider = ""Akka.Cluster.ClusterActorRefProvider, Akka.Cluster""
                    serializers {
                      wire = ""Akka.Serialization.WireSerializer, Akka.Serialization.Wire""
                      proto = ""Akka.Interfaced.ProtobufSerializer.ProtobufSerializer, Akka.Interfaced.ProtobufSerializer""
                    }
                    serialization-bindings {
                      ""Akka.Interfaced.NotificationMessage, Akka.Interfaced-Base"" = proto
                      ""Akka.Interfaced.RequestMessage, Akka.Interfaced-Base"" = proto
                      ""Akka.Interfaced.ResponseMessage, Akka.Interfaced-Base"" = proto
                      ""System.Object"" = wire
                    }
                  }
                  remote {
                    helios.tcp {
                      hostname = ""127.0.0.1""
                    }
                  }
                  cluster {
                    seed-nodes = [""akka.tcp://GameCluster@127.0.0.1:3001""]
                    auto-down-unreachable-after = 30s
                  }
                }");

            return new ClusterRunner(commonConfig);
        }
    }
}
