using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Win32;
using System.IO;
using System.Diagnostics;
using System.ServiceProcess;
using System.Threading;

namespace OpenVpn
{
    public class OpenVpnService : ServiceBase
    {
        public static string DefaultServiceName = "OpenVpnService";
        private OpenVPNServiceRunner _serviceRunner;
        private EventLog _eventLog;

        public OpenVpnService()
        {
            this.ServiceName = DefaultServiceName;
            this.CanStop = true;
            this.CanPauseAndContinue = false;
            // N.B. if OpenVPN always dies when suspending, then this is unnecessary
            // However if there is some kind of stuck state where OpenVPN.exe hangs
            // after resuming, then this will help
            this.CanHandlePowerEvent = false;
            this.AutoLog = true;

            _eventLog = new EventLog();
            if (!EventLog.SourceExists(this.ServiceName))
            {
                EventLog.CreateEventSource(this.ServiceName, "Application");
            }
            _eventLog.Source = this.ServiceName;
            _eventLog.Log = "Application";

            _serviceRunner = new OpenVPNServiceRunner(_eventLog); // Decoupled service logic
        }

        protected override void OnStart(string[] args)
        {
            _serviceRunner.Start(args);
        }

        protected override void OnStop()
        {
            RequestAdditionalTime(3000);
            _serviceRunner.Stop();
        }
    }
}
