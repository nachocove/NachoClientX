// This file is generated mk_log_settings.py. You can customize the log settings.
namespace NachoCore.Utils
{
    public partial class LogSettings {
        public const ulong CONSOLE_SETTINGS = 0;
        // WBXML and state machines have specialized telemetry API. So, their
        // logs are not sent to telemetry.
        public const ulong TELEMETRY_SETTINGS = 0xffffffffffffffff & ~Log.LOG_XML;
        // Default caller info is disabled everywhere. Enabling it adds
        // file and line number to logs but also slows down logging a bit.
        public const bool CALLERINFO = false;

        #if (DEBUG)
        public const ulong DEBUG_CONSOLE_SETTINGS = CONSOLE_SETTINGS;
        #else
        // Release builds do not send debug logs to console.
        public const ulong DEBUG_CONSOLE_SETTINGS = 0;
        #endif
        public const ulong INFO_CONSOLE_SETTINGS = CONSOLE_SETTINGS;
        public const ulong WARN_CONSOLE_SETTINGS = CONSOLE_SETTINGS;
        public const ulong ERROR_CONSOLE_SETTINGS = CONSOLE_SETTINGS;

        public const ulong DEBUG_TELEMETRY_SETTINGS = TELEMETRY_SETTINGS;
        public const ulong INFO_TELEMETRY_SETTINGS = TELEMETRY_SETTINGS;
        public const ulong WARN_TELEMETRY_SETTINGS = TELEMETRY_SETTINGS;
        public const ulong ERROR_TELEMETRY_SETTINGS = TELEMETRY_SETTINGS;

        public const bool DEBUG_CALLERINFO = CALLERINFO;
        public const bool INFO_CALLERINFO = CALLERINFO;
        public const bool WARN_CALLERINFO = CALLERINFO;
        public const bool ERROR_CALLERINFO = CALLERINFO;
    }
}
