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

    class OpenVpnService : System.ServiceProcess.ServiceBase
    {
        public static string DefaultServiceName = "OpenVpnService";

        public const string Package = "openvpn";
        private List<OpenVpnChild> Subprocesses;

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

            this.Subprocesses = new List<OpenVpnChild>();
        }

        protected override void OnStop()
        {
            RequestAdditionalTime(3000);
            foreach (var child in Subprocesses)
            {
                child.SignalProcess();
            }
            // Kill all processes -- wait for 2500 msec at most
            DateTime tEnd = DateTime.Now.AddMilliseconds(2500.0);
            foreach (var child in Subprocesses)
            {
               int timeout = (int) (tEnd - DateTime.Now).TotalMilliseconds;
               child.StopProcess(timeout > 0 ? timeout : 0);
            }
        }

        private RegistryKey GetRegistrySubkey(RegistryView rView)
        {
            try
            {
                return RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, rView)
                    .OpenSubKey("Software\\OpenVPN");
            }
            catch (ArgumentException)
            {
                return null;
            }
            catch (NullReferenceException)
            {
                return null;
            }
        }

        protected override void OnStart(string[] args)
        {
            try
            {
                List<RegistryKey> rkOvpns = new List<RegistryKey>();

                // Search 64-bit registry, then 32-bit registry for OpenVpn
                var key = GetRegistrySubkey(RegistryView.Registry64);
                if (key != null) rkOvpns.Add(key);
                key = GetRegistrySubkey(RegistryView.Registry32);
                if (key != null) rkOvpns.Add(key);

                if (rkOvpns.Count() == 0)
                    throw new Exception("Registry key missing");

                var configDirsConsidered = new HashSet<string>();

                foreach (var rkOvpn in rkOvpns)
                {
                    try {
                        bool append = false;
                        {
                            var logAppend = (string)rkOvpn.GetValue("log_append");
                            if (logAppend[0] == '0' || logAppend[0] == '1')
                                append = logAppend[0] == '1';
                            else
                                throw new Exception("Log file append flag must be 1 or 0");
                        }

                        var config = new OpenVpnServiceConfiguration()
                        {
                            exePath = (string)rkOvpn.GetValue("exe_path"),
                            configDir = (string)rkOvpn.GetValue("autostart_config_dir"),
                            configExt = "." + (string)rkOvpn.GetValue("config_ext"),
                            logDir = (string)rkOvpn.GetValue("log_dir"),
                            logAppend = append,
                            priorityClass = GetPriorityClass((string)rkOvpn.GetValue("priority")),

                            eventLog = EventLog,
                        };

                        if (String.IsNullOrEmpty(config.configDir) || configDirsConsidered.Contains(config.configDir)) {
                            continue;
                        }
                        configDirsConsidered.Add(config.configDir);

                        /// Only attempt to start the service
                        /// if openvpn.exe is present. This should help if there are old files
                        /// and registry settings left behind from a previous OpenVPN 32-bit installation
                        /// on a 64-bit system.
                        if (!File.Exists(config.exePath))
                        {
                            EventLog.WriteEntry("OpenVPN binary does not exist at " + config.exePath);
                            continue;
                        }

                        foreach (var configFilename in Directory.EnumerateFiles(config.configDir,
                                                                                "*" + config.configExt,
                                                                                System.IO.SearchOption.AllDirectories))
                        {
                            try {
                                var child = new OpenVpnChild(config, configFilename);
                                Subprocesses.Add(child);
                                child.Start();
                            }
                            catch (Exception e)
                            {
                                EventLog.WriteEntry("Caught exception " + e.Message + " when starting openvpn for "
                                    + configFilename);
                            }
                        }
                    }
                    catch (NullReferenceException e) /* e.g. missing registry values */
                    {
                        EventLog.WriteEntry("Registry values are incomplete for " + rkOvpn.View.ToString() + e.StackTrace);
                    }
                }

            }
            catch (Exception e)
            {
                EventLog.WriteEntry("Exception occured during OpenVPN service start: " + e.Message + e.StackTrace);
                throw e;
            }
        }

        private System.Diagnostics.ProcessPriorityClass GetPriorityClass(string priorityString)
        {
            if (String.Equals(priorityString, "IDLE_PRIORITY_CLASS", StringComparison.InvariantCultureIgnoreCase)) {
                return System.Diagnostics.ProcessPriorityClass.Idle;
            }
            else if (String.Equals(priorityString, "BELOW_NORMAL_PRIORITY_CLASS", StringComparison.InvariantCultureIgnoreCase))
            {
                return System.Diagnostics.ProcessPriorityClass.BelowNormal;
            }
            else if (String.Equals(priorityString, "NORMAL_PRIORITY_CLASS", StringComparison.InvariantCultureIgnoreCase))
            {
                return System.Diagnostics.ProcessPriorityClass.Normal;
            }
            else if (String.Equals(priorityString, "ABOVE_NORMAL_PRIORITY_CLASS", StringComparison.InvariantCultureIgnoreCase))
            {
                return System.Diagnostics.ProcessPriorityClass.AboveNormal;
            }
            else if (String.Equals(priorityString, "HIGH_PRIORITY_CLASS", StringComparison.InvariantCultureIgnoreCase))
            {
                return System.Diagnostics.ProcessPriorityClass.High;
            }
            else {
                throw new Exception("Unknown priority name: " + priorityString);
            }
        }
    }
}
