using System;
using System.IO;
using System.Text;

namespace SeerNote.Cli
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            Console.InputEncoding = new UTF8Encoding(false);
            Console.OutputEncoding = new UTF8Encoding(false);
            return CliApplication.Run(args, Console.In, Console.Out, Console.Error, Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory));
        }
    }
}
