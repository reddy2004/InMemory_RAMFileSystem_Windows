using System;
using DokanNet;
using DokanNet.Logging;
using Newtonsoft.Json;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Threading;

namespace REDFS_ClusterMode
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("Hello World! (Yes, we should add this even now!)");
            try
            {
                /*
                 * This should be done by HTTP server thread
                 */ 
                IncoreFSSkeleton ifs = new IncoreFSSkeleton();
                ifs.Mount(@"N:\", /*DokanOptions.DebugMode | DokanOptions.EnableNotificationAPI*/ DokanOptions.FixedDrive, /*treadCount=*/5, new NullLogger());
            }
            catch (DokanException ex)
            {
                Console.WriteLine(@"Error: " + ex.Message);
            }
        }
    }
}
