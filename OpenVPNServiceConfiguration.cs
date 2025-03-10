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

        public EventLog eventLog { get; set; }
    }
}
