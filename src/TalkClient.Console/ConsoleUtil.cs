using System;

namespace TalkClient.Console
{
    using Console = System.Console;

    public static class ConsoleUtil
    {
        public static void Out(string str)
        {
            Console.WriteLine(str);
        }

        public static void Sys(string str)
        {
            var c = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("* " + str);
            Console.ForegroundColor = c;
        }

        public static void Err(string str)
        {
            var c = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("! " + str);
            Console.ForegroundColor = c;
        }
    }
}
