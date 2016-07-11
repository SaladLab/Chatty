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

                x.SetServiceName("Chatty");
                x.SetDisplayName("Chatty Service");
                x.SetDescription("Chatty Service using Akka.NET and Akka.Interfaced. (https://github.com/SaladLab/Chatty)");

                x.UseAssemblyInfoForServiceInfo();
                x.RunAsLocalSystem();
                x.StartAutomatically();
                x.Service(() => new TalkService(runner));
                x.EnableServiceRecovery(r => r.RestartService(1));
            });
        }
    }
}
