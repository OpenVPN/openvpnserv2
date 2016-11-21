using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Win32;
using System.IO;
using System.Diagnostics;
using System.ServiceProcess;
using System.Runtime.InteropServices;

namespace OpenVpn
{
    class SysWin32
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "CreateEventW", SetLastError = true)]
        public static extern IntPtr CreateEvent(IntPtr lpEventAttributes, bool bManualReset, bool bInitialState, string lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetEvent(IntPtr hEvent);
    }

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
                            configDir = (string)rkOvpn.GetValue("config_dir"),
                            configExt = "." + (string)rkOvpn.GetValue("config_ext"),
                            logDir = (string)rkOvpn.GetValue("log_dir"),
                            logAppend = append,
                            priorityClass = GetPriorityClass((string)rkOvpn.GetValue("priority")),

                            eventLog = EventLog,
                        };

                        if (configDirsConsidered.Contains(config.configDir)) {
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

        public static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                Run(new OpenVpnService());
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

    class OpenVpnServiceConfiguration {
        public string exePath {get;set;}
        public string configExt {get;set;}
        public string configDir {get;set;}
        public string logDir {get;set;}
        public bool logAppend {get;set;}
        public System.Diagnostics.ProcessPriorityClass priorityClass {get;set;}
        
        public EventLog eventLog {get;set;}
    }
    
    class OpenVpnChild {
        StreamWriter logFile;
        Process process;
        ProcessStartInfo startInfo;
        System.Timers.Timer restartTimer;
        OpenVpnServiceConfiguration config;
        string configFile;
        string exitEvent;
    
        public OpenVpnChild(OpenVpnServiceConfiguration config, string configFile) {
            this.config = config;
            /// SET UP LOG FILES
            /* Because we will be using the filenames in our closures,
             * so make sure we are working on a copy */
            this.configFile = String.Copy(configFile);
            this.exitEvent = Path.GetFileName(configFile) + "_" + Process.GetCurrentProcess().Id.ToString();
            var justFilename = System.IO.Path.GetFileName(configFile);
            var logFilename = config.logDir + "\\" +
                    justFilename.Substring(0, justFilename.Length - config.configExt.Length) + ".log";
            
            // FIXME: if (!init_security_attributes_allow_all (&sa))
            //{
            //    MSG (M_SYSERR, "InitializeSecurityDescriptor start_" PACKAGE " failed");
            //    goto finish;
            //}
            
            logFile = new StreamWriter(File.Open(logFilename,
                config.logAppend ? FileMode.Append : FileMode.Create,
                FileAccess.Write,
                FileShare.Read), new UTF8Encoding(false));
            logFile.AutoFlush = true;
            
            /// SET UP PROCESS START INFO
            string[] procArgs = {
                "--config",
                "\"" + configFile + "\"",
                "--service ",
                "\"" + exitEvent + "\"" + " 0"
            };
            this.startInfo = new System.Diagnostics.ProcessStartInfo()
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,

                FileName = config.exePath,
                Arguments = String.Join(" ", procArgs),
                WorkingDirectory = config.configDir,

                UseShellExecute = false,
                /* create_new_console is not exposed -- but we probably don't need it?*/
            };
        }
        
        // set exit event so that openvpn will terminate
        public void SignalProcess() {
            if (restartTimer != null) {
                restartTimer.Stop();
            }
            try
            {
                if (!process.HasExited)
                {
                    var h = SysWin32.CreateEvent(IntPtr.Zero, true, false, exitEvent);
                    if (h != IntPtr.Zero)
                    {
                        process.Exited -= Watchdog; // Don't restart the process after exit
                        SysWin32.SetEvent(h);
                        SysWin32.CloseHandle(h);
                    }
                    else
                    {
                        var e = Marshal.GetLastWin32Error();
                        config.eventLog.WriteEntry ("Error creating exit event named '" + exitEvent
                                                    + "' (error code = " + e + ")", EventLogEntryType.Error);
                    }
                }
            }
            catch (InvalidOperationException) { }
        }

        // terminate process after a timeout
        public void StopProcess(int timeout) {
            if (restartTimer != null) {
                restartTimer.Stop();
            }
            try
            {
                if (!process.WaitForExit(timeout))
                {
                    process.Exited -= Watchdog; // Don't restart the process after kill
                    process.Kill();
                }
            }
            catch (InvalidOperationException) { }
        }
        
        public void Wait() {
            process.WaitForExit();
            logFile.Close();
        }

        public void Restart() {
            if (restartTimer != null) {
                restartTimer.Stop();
            }
            /* try-catch... because there could be a concurrency issue (write-after-read) here? */
            if (!process.HasExited)
            {
                process.Exited -= Watchdog;
                process.Exited += FastRestart; // Restart the process after kill
                try
                {
                    process.Kill();
                }
                catch (InvalidOperationException)
                {
                    Start();
                }
            }
            else
            {
                Start();
            }
        }

        private void WriteToLog(object sendingProcess, DataReceivedEventArgs e) {
            if (e != null)
                logFile.WriteLine(e.Data);
        }

        /// Restart after 10 seconds
        /// For use with unexpected terminations
        private void Watchdog(object sender, EventArgs e)
        {
            config.eventLog.WriteEntry("Process for " + configFile + " exited. Restarting in 10 sec.");

            restartTimer = new System.Timers.Timer(10000);
            restartTimer.AutoReset = false;
            restartTimer.Elapsed += (object source, System.Timers.ElapsedEventArgs ev) =>
                {
                    Start();
                };
            restartTimer.Start();
        }

        /// Restart after 3 seconds
        /// For use with Restart() (e.g. after a resume)
        private void FastRestart(object sender, EventArgs e)
        {
            config.eventLog.WriteEntry("Process for " + configFile + " restarting in 3 sec");
            restartTimer = new System.Timers.Timer(3000);
            restartTimer.AutoReset = false;
            restartTimer.Elapsed += (object source, System.Timers.ElapsedEventArgs ev) =>
                {
                    Start();
                };
            restartTimer.Start();
        }
        
        public void Start() {
            process = new System.Diagnostics.Process();

            process.StartInfo = startInfo;
            process.EnableRaisingEvents = true;

            process.OutputDataReceived += WriteToLog;
            process.ErrorDataReceived += WriteToLog;
            process.Exited += Watchdog;

            process.Start();
            process.BeginErrorReadLine();
            process.BeginOutputReadLine();
            process.PriorityClass = config.priorityClass;        
        }
    
    }
}
