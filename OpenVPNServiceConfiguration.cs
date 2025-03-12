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

        /// <summary>
        /// Delegate used to log messages with a specified severity level.
        /// </summary>
        public Action<string, EventLogEntryType> Log;

        /// <summary>
        /// Constructs OpenVpnServiceConfiguration object
        /// </summary>
        /// <param name="logAction">Log callback</param>
        public OpenVpnServiceConfiguration(Action<string, EventLogEntryType> logAction)
        {
            Log = logAction;
        }

        /// <summary>
        /// Writes log message via log callback
        /// </summary>
        /// <param name="message"></param>
        /// <param name="type"></param>
        public void LogMessage(string message, EventLogEntryType type = EventLogEntryType.Information)
        {
            Log(message, type);
        }
    }
}
