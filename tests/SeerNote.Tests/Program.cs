using System;
using System.Text;

namespace SeerNote.Tests
{
    internal static class Program
    {
        [STAThread]
        private static int Main()
        {
            Console.OutputEncoding = new UTF8Encoding(false);
            try
            {
                DomainTests.RunAll();
                Console.WriteLine("PASS DomainTests");
                StorageTests.RunAll();
                Console.WriteLine("PASS StorageTests");
                ThemeTests.RunAll();
                Console.WriteLine("PASS ThemeTests");
                ApplicationTests.RunAll();
                Console.WriteLine("PASS ApplicationTests");
                CliTests.RunAll();
                Console.WriteLine("PASS CliTests");
                PresentationTests.RunAll();
                Console.WriteLine("PASS PresentationTests");
                Console.WriteLine("ALL_TESTS_PASSED");
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine("TEST_FAILED");
                Console.Error.WriteLine(error);
                return 1;
            }
        }
    }
}
