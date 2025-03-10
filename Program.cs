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
            if (args.Length == 0)
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
            }
            else if (args[0] == "-install")
            {
                try
                {
                    ProjectInstaller.Install();
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e.Message);
                    Console.Error.WriteLine(e.StackTrace);
                    return 1;
                }
            }
            else if (args[0] == "-remove")
            {
                try
                {
                    ProjectInstaller.Stop();
                    ProjectInstaller.Uninstall();
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e.Message);
                    Console.Error.WriteLine(e.StackTrace);
                    return 1;
                }
            }
            else
            {
                Console.Error.WriteLine("Unknown command: " + args[0]);
                return 1;
            }
            return 0;
        }
    }
}
