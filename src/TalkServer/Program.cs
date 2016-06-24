using System;
using System.Threading;
using System.Threading.Tasks;

namespace TalkServer
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var service = new TalkService();
            var cts = new CancellationTokenSource();
            var runTask = Task.Run(() => service.RunAsync(args, cts.Token));

            Console.WriteLine("Enter to stop system.");
            Console.ReadLine();

            cts.Cancel();
            runTask.Wait();
        }
    }
}
