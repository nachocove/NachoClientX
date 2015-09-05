#!/usr/bin/env python
import os.path


def create_log_options():
    path = 'LogSettings.cs'
    if os.path.exists(path):
        print '%s already exists' % path
        return  # file already exists. don't overwrite it.
    with open(path, 'w') as f:
        print 'Creating %s...' % path
        print >>f, '// This file is generated mk_log_settings.py. You can customize the log settings.'
        print >>f, 'namespace NachoCore.Utils'
        print >>f, '{'
        print >>f, '    public partial class LogSettings {'
        print >>f, '        public const ulong CONSOLE_SETTINGS = 0xffffffffffffffff & ~Log.LOG_XML;'
        print >>f, '        // WBXML and state machines have specialized telemetry API. So, their'
        print >>f, '        // logs are not sent to telemetry.'
        print >>f, '        public const ulong TELEMETRY_SETTINGS = 0xffffffffffffffff & ~Log.LOG_XML;'
        print >>f, '        // Default caller info is disabled everywhere. Enabling it adds'
        print >>f, '        // file and line number to logs but also slows down logging a bit.'
        print >>f, '        public const bool CALLERINFO = false;'
        print >>f, ''
        print >>f, '        // Info messages should go to the console in dev and alpha build, but not in beta or production.'
        print >>f, '        // There are two different settings. The correct one wil be picked at runtime.'
        print >>f, '        public const ulong DEBUG_CONSOLE_SETTINGS = 0;'
        print >>f, '        public const ulong INFO_CONSOLE_SETTINGS = 0;'
        print >>f, '        public const ulong INFO_DEV_CONSOLE_SETTINGS = CONSOLE_SETTINGS;'
        print >>f, '        public const ulong WARN_CONSOLE_SETTINGS = CONSOLE_SETTINGS;'
        print >>f, '        public const ulong ERROR_CONSOLE_SETTINGS = CONSOLE_SETTINGS;'
        print >>f, ''
        print >>f, '        public const ulong DEBUG_TELEMETRY_SETTINGS = 0;'
        print >>f, '        public const ulong INFO_TELEMETRY_SETTINGS = TELEMETRY_SETTINGS;'
        print >>f, '        public const ulong WARN_TELEMETRY_SETTINGS = TELEMETRY_SETTINGS;'
        print >>f, '        public const ulong ERROR_TELEMETRY_SETTINGS = TELEMETRY_SETTINGS;'
        print >>f, ''
        print >>f, '        public const bool DEBUG_CALLERINFO = CALLERINFO;'
        print >>f, '        public const bool INFO_CALLERINFO = CALLERINFO;'
        print >>f, '        public const bool WARN_CALLERINFO = CALLERINFO;'
        print >>f, '        public const bool ERROR_CALLERINFO = CALLERINFO;'
        print >>f, '    }'
        print >>f, '}'


if __name__ == '__main__':
    create_log_options()
