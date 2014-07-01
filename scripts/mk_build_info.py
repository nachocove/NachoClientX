#!/usr/bin/env python

# This script creates a BuildInfo.cs that contains information about build time / setting.
# The .cs file provides the following information:
#
# 1. Version - E.g. 1.4.1. For a development build, it is changed to
#              DEV[<username>]; e.g. DEV[henry]
# 2. Build # - A integer that increases after every build. A .build_number file is created
#              at the root of the project directory that holds the last build number.
# 3. Build time - Time that BuildInfo.cs is generated.
# 4. Build machine - Machine that is used to build this.
# 5. User - Username that starts the build.
# 6. Source - git workspace information.

from argparse import ArgumentParser
import sys
import os
import xml.sax
from datetime import datetime
import subprocess
from xml.sax.handler import ContentHandler


def get_username():
    return os.getlogin()


def create_buildinfo(options):
    version = determine_version(options.csproj_file)
    build_number = determine_build(options.root)
    path = os.path.join(options.root, 'BuildInfo.cs')
    if version.startswith('DEV['):
        # For development builds, we include git info
        source = subprocess.check_output(['git', 'log', '-1', '--pretty=format:%H (%ai by %cn)'])
    else:
        source = None
    with open(path, 'w') as f:
        print >>f, '// This file is generated by mk_build_info.py. DO NOT EDIT.'
        print >>f, '//'
        print >>f, '// Command-line parameters'
        print >>f, '//     %s' % ' '.join(sys.argv[1:])
        print >>f, '//'
        if 'BUILD' in os.environ or 'VERSION' in os.environ:
            print >>f, '// Environment variables'
            if 'RELEASE' in os.environ:
                print >>f, '//     RELEASE=%s' % os.environ['RELEASE']
            if 'BUILD' in os.environ:
                print >>f, '//     BUILD=%s' % os.environ['BUILD']
        print >>f, 'namespace NachoClient.Build'
        print >>f, '{'
        print >>f, '    public class BuildInfo'
        print >>f, '    {'
        print >>f, '        public const string Version = "%s";' % version
        print >>f, '        public const string BuildNumber = "%s";' % build_number
        print >>f, '        public const string Time = "%s";' % datetime.now().strftime('%m/%d/%Y %H:%M:%S')
        print >>f, '        public const string User = "%s";' % get_username()
        if source is not None:
            print >>f, '        public const string Source = "%s";' % source
        print >>f, '    }'
        print >>f, '}'


class CsprojParser(ContentHandler):
    def __init__(self, xml_file):
        ContentHandler.__init__(self)
        # Xamarin Studio does not generate a <ReleaseVersion> element
        # if the version is 0.1.
        self.version = '0.1'
        self.data = ''
        xml.sax.parse(xml_file, self)

    def startElement(self, name, attrs):
        self.data = ''

    def characters(self, content):
        self.data += content

    def endElement(self, name):
        if name == u'ReleaseVersion':
            self.version = str(self.data)


def determine_version(csproj_file):
    # If a version is provided via environment, use that
    if 'VERSION' in os.environ:
        return os.environ['VERSION']

    if 'BUILD' in os.environ:
        # For production (if BUILD is provided), extract it from .csproj
        return CsprojParser(csproj_file).version
    else:
        # Dev build
        return 'DEV[%s]' % get_username()


def determine_build(proj_root):
    if 'BUILD' in os.environ:
        # If production build, the build # must be provided.
        return os.environ['BUILD']

    # For dev build, extract the number from .build_number
    path = os.path.join(proj_root, '.build_number')
    if os.path.exists(path):
        with open(path, 'r') as f:
            build_number = int(f.readline())
        build_number += 1
    else:
        build_number = 1

    # Write the build number to a new build number file.
    # When the build is successfully complete, the new file replaces
    # .build_number. This way, the build number only advances on successful
    # builds and we will have build number gaps in telemetry. It is hard to
    # determine whether those gaps are from build failures or telemetry bugs.
    new_path = path + '.new'
    with open(new_path, 'w') as f:
        print >> f, build_number

    return str(build_number)


def main():
    parser = ArgumentParser()
    parser.add_argument('--csproj-file', help='Xamarin project file', default=None)
    parser.add_argument('--root', help='Root directory of the project', default=None)
    options = parser.parse_args()

    create_buildinfo(options)

if __name__ == '__main__':
    main()