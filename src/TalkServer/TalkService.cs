using System;
using System.Configuration;
using Aim.ClusterNode;
using Akka.Configuration.Hocon;
using Domain;
using Topshelf;

namespace TalkServer
{
    public class TalkService : ServiceControl
    {
        private ClusterRunner _clusterRunner;
        private string _runner;

        public TalkService(string runner)
        {
            _runner = runner;
        }

        bool ServiceControl.Start(HostControl hostControl)
        {
            // force interface assembly to be loaded before creating ProtobufSerializer

            var type = typeof(IUser);
            if (type == null)
                throw new InvalidProgramException("!");

            // connect to redis

            try
            {
                var cstr = ConfigurationManager.ConnectionStrings["Redis"].ConnectionString;
                RedisStorage.Instance = new RedisStorage(cstr);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error in connecting redis server: " + e);
                return false;
            }

            // run cluster nodes

            var section = (AkkaConfigurationSection)ConfigurationManager.GetSection("akka");
            var config = section.AkkaConfig;
            var runner = new ClusterRunner(config, new[] { GetType().Assembly });
            runner.CreateClusterNodeContext = () => new ClusterNodeContext();

            var runnerConfig = config.GetValue("system.runner").GetObject();
            var nodes = runnerConfig.GetKey(_runner ?? "default");
            if (nodes == null)
                throw new InvalidOperationException("Cannot find runner: " + _runner);

            runner.Launch(nodes.GetArray()).Wait();
            _clusterRunner = runner;

            return true;
        }

        bool ServiceControl.Stop(HostControl hostControl)
        {
            if (_clusterRunner != null)
            {
                _clusterRunner.Shutdown().Wait();
                _clusterRunner = null;
            }
            return true;
        }
    }
}
