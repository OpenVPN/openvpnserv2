using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace OpenVpn
{ 
    internal class Program
    {
        public static int Main(string[] args)
        {
            if (!Environment.UserInteractive)
            {
                // Running as a Windows Service
                ServiceBase.Run(new OpenVpnService());
            }
            else
            {
                // Running as a console application
                Console.WriteLine("Running in console mode...");
                var runner = new OpenVPNServiceRunner(null);
                runner.Start(args);

                Console.WriteLine("Press Enter to stop...");
                Console.ReadLine();

                runner.Stop();
            }
            return 0;
        }
    }
}
