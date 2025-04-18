﻿using Microsoft.Win32;
using OpenVpn;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace OpenVpn
{
    /// <summary>
    /// Main class implementing OpenVPN service functionality,
    /// without dependencies to Windows service infrastructure
    /// </summary>
    class OpenVPNServiceRunner
    {
        private List<OpenVpnChild> Subprocesses;
        private EventLog _eventLog;

        /// <summary>
        /// Creates OpenVPNServiceRunner object
        /// </summary>
        /// <param name="eventLog">EventLog or null</param>
        public OpenVPNServiceRunner(EventLog eventLog)
        {
            this.Subprocesses = new List<OpenVpnChild>();
            _eventLog = eventLog;
        }

        /// <summary>
        /// Stops all OpenVPN child processes
        /// </summary>
        public void Stop()
        {
            foreach (var child in Subprocesses)
            {
                child.SignalProcess();
            }
        }

        /// <summary>
        /// Gets registry subkey
        /// </summary>
        /// <param name="rView"></param>
        /// <returns>Registry key, null if not found</returns>
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

        /// <summary>
        /// Reads configuration from the registry and starts OpenVPN process for every ovpn config found.
        /// </summary>
        /// <param name="args"></param>
        public void Start(string[] args)
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
                    try
                    {
                        bool append = false;
                        {
                            var logAppend = (string)rkOvpn.GetValue("log_append");
                            if (logAppend[0] == '0' || logAppend[0] == '1')
                                append = logAppend[0] == '1';
                            else
                                throw new Exception("Log file append flag must be 1 or 0");
                        }

                        var config = new OpenVpnServiceConfiguration(Log)
                        {
                            exePath = (string)rkOvpn.GetValue("exe_path"),
                            configDir = (string)rkOvpn.GetValue("autostart_config_dir"),
                            configExt = "." + (string)rkOvpn.GetValue("config_ext"),
                            logDir = (string)rkOvpn.GetValue("log_dir"),
                            logAppend = append
                        };

                        if (String.IsNullOrEmpty(config.configDir) || configDirsConsidered.Contains(config.configDir))
                        {
                            continue;
                        }
                        configDirsConsidered.Add(config.configDir);

                        /// Only attempt to start the service
                        /// if openvpn.exe is present. This should help if there are old files
                        /// and registry settings left behind from a previous OpenVPN 32-bit installation
                        /// on a 64-bit system.
                        if (!File.Exists(config.exePath))
                        {
                            Log("OpenVPN binary does not exist at " + config.exePath);
                            continue;
                        }

                        foreach (var configFilename in Directory.EnumerateFiles(config.configDir,
                                                                                "*" + config.configExt,
                                                                                System.IO.SearchOption.AllDirectories))
                        {
                            try
                            {
                                var child = new OpenVpnChild(config, configFilename);
                                Subprocesses.Add(child);
                                child.Start();
                            }
                            catch (Exception e)
                            {
                                Log("Caught exception " + e.Message + " when starting openvpn for "
                                    + configFilename, EventLogEntryType.Error);
                            }
                        }
                    }
                    catch (NullReferenceException e) /* e.g. missing registry values */
                    {
                        Log("Registry values are incomplete for " + rkOvpn.View.ToString() + e.StackTrace, EventLogEntryType.Error);
                    }
                }

            }
            catch (Exception e)
            {
                Log("Exception occured during OpenVPN service start: " + e.Message + e.StackTrace, EventLogEntryType.Error);
                throw e;
            }
        }

        /// <summary>
        /// Writes log message either to event log or console
        /// </summary>
        /// <param name="message"></param>
        /// <param name="type"></param>
        private void Log(string message, EventLogEntryType type = EventLogEntryType.Information)
        {
            if (_eventLog != null)
            {
                _eventLog.WriteEntry(message, type);
            }
            else
            {
                Console.WriteLine($"[{type}] {message}");
            }
        }
    }
}
