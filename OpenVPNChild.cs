using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OpenVpn
{
    class OpenVpnChild
    {
        StreamWriter logFile;
        Process process;
        ProcessStartInfo startInfo;
        System.Timers.Timer restartTimer;
        OpenVpnServiceConfiguration config;
        string configFile;
        string exitEvent;

        public OpenVpnChild(OpenVpnServiceConfiguration config, string configFile)
        {
            this.config = config;
            this.configFile = configFile;
            this.exitEvent = Path.GetFileName(configFile) + "_" + Process.GetCurrentProcess().Id.ToString();

            var justFilename = System.IO.Path.GetFileName(configFile);
            var logFilename = config.logDir + "\\" +
                    justFilename.Substring(0, justFilename.Length - config.configExt.Length) + ".log";

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
        public void SignalProcess()
        {
            if (restartTimer != null)
            {
                restartTimer.Stop();
            }
            try
            {
                if (!process.HasExited)
                {

                    try
                    {
                        var waitHandle = new EventWaitHandle(false, EventResetMode.ManualReset, exitEvent);

                        process.Exited -= Watchdog; // Don't restart the process after exit

                        waitHandle.Set();
                        waitHandle.Close();
                    }
                    catch (IOException e)
                    {
                        config.LogMessage("IOException creating exit event named '" + exitEvent + "' " + e.Message + e.StackTrace, EventLogEntryType.Error);
                    }
                    catch (UnauthorizedAccessException e)
                    {
                        config.LogMessage("UnauthorizedAccessException creating exit event named '" + exitEvent + "' " + e.Message + e.StackTrace, EventLogEntryType.Error);
                    }
                    catch (WaitHandleCannotBeOpenedException e)
                    {
                        config.LogMessage("WaitHandleCannotBeOpenedException creating exit event named '" + exitEvent + "' " + e.Message + e.StackTrace, EventLogEntryType.Error);
                    }
                    catch (ArgumentException e)
                    {
                        config.LogMessage("ArgumentException creating exit event named '" + exitEvent + "' " + e.Message + e.StackTrace, EventLogEntryType.Error);
                    }
                }
            }
            catch (InvalidOperationException) { }
        }

        // terminate process after a timeout
        public void StopProcess(int timeout)
        {
            if (restartTimer != null)
            {
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

        public void Wait()
        {
            process.WaitForExit();
            logFile.Close();
        }

        private void WriteToLog(object sendingProcess, DataReceivedEventArgs e)
        {
            if (e != null)
                logFile.WriteLine(e.Data);
        }

        /// Restart after 10 seconds
        /// For use with unexpected terminations
        private void Watchdog(object sender, EventArgs e)
        {
            config.LogMessage("Process for " + configFile + " exited. Restarting in 10 sec.");

            restartTimer = new System.Timers.Timer(10000);
            restartTimer.AutoReset = false;
            restartTimer.Elapsed += (object source, System.Timers.ElapsedEventArgs ev) =>
            {
                Start();
            };
            restartTimer.Start();
        }

        public void Start()
        {
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
