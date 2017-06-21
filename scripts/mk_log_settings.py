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
        print >>f, 'using System.Collections.Generic;'
        print >>f, 'namespace NachoCore.Utils'
        print >>f, '{'
        print >>f, '    public partial class LogSettings {'
        print >>f, '        public static Dictionary<string, Levels> Subsystems = new Dictionary<string, Levels> {'
        print >>f, '            {"XML", new Levels {Console = Log.Level.Off, Telemetry = Log.Level.Off}}'
        print >>f, '        };'
        print >>f, '    }'
        print >>f, '}'


if __name__ == '__main__':
    create_log_options()
