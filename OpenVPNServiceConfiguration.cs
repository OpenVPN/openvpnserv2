using System;
using System.Diagnostics;

namespace OpenVpn
{
    class OpenVpnServiceConfiguration
    {
        public string exePath { get; set; }
        public string configExt { get; set; }
        public string configDir { get; set; }
        public string logDir { get; set; }
        public bool logAppend { get; set; }
        public System.Diagnostics.ProcessPriorityClass priorityClass { get; set; }

        public Action<string, EventLogEntryType> Log; // Delegate for logging

        public OpenVpnServiceConfiguration(Action<string, EventLogEntryType> logAction)
        {
            Log = logAction;
        }
        public void LogMessage(string message, EventLogEntryType type = EventLogEntryType.Information)
        {
            Log(message, type);
        }
    }
}
