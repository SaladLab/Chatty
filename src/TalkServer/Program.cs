using System;
using Topshelf;

namespace TalkServer
{
    internal class Program
    {
        private static int Main(string[] args)
        {
            return (int)HostFactory.Run(x =>
            {
                string runner = null;
                x.AddCommandLineDefinition("runner", val => runner = val);

                x.SetServiceName("TalkServer");
                x.SetDisplayName("TalkServer for Chatty");
                x.SetDescription("TalkServer for Chatty using Akka.NET and Akka.Interfaced.");

                x.UseAssemblyInfoForServiceInfo();
                x.RunAsLocalSystem();
                x.StartAutomatically();
                x.Service(() => new TalkService(runner));
                x.EnableServiceRecovery(r => r.RestartService(1));
            });
        }
    }
}
