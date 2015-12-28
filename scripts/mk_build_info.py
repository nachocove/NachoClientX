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
import collections
from xml.sax.handler import ContentHandler
from projects import projects


class BuildInfoFile:
    def __init__(self):
        self.entries = collections.OrderedDict()

    def write(self, path):
        """
        Generate BuildInfo.cs
        """
        with open(path, 'w') as f:
            print >>f, '// This file is generated by mk_build_info.py. DO NOT EDIT.'
            print >>f, '//'
            print >>f, '// Command-line parameters'
            print >>f, '//     %s' % ' '.join(sys.argv[1:])
            print >>f, '//'
            if 'BUILD' in os.environ or 'VERSION' in os.environ:
                print >>f, '// Environment variables'
                if 'VERSION' in os.environ:
                    print >>f, '//     VERSION=%s' % os.environ['VERSION']
                if 'BUILD' in os.environ:
                    print >>f, '//     BUILD=%s' % os.environ['BUILD']
            print >>f, 'namespace NachoClient.Build'
            print >>f, '{'
            print >>f, '    public class BuildInfo'
            print >>f, '    {'
            for (key, value) in self.entries.items():
                print >>f, '        public const string %s = @"%s";' % (key, value)
            print >>f, '    }'
            print >>f, '}'

    def add(self, key, value):
        self.entries[key] = value


def get_username():
    return os.getlogin()


def create_buildinfo(options):
    build_info = BuildInfoFile()
    version = determine_version(options.csproj_file)
    build_number = determine_build(options.root)
    path = os.path.join(options.root, 'BuildInfo.cs')
    if version.startswith('DEV['):
        # For development builds, we include git info
        source = subprocess.check_output(['git', 'log', '-1', '--pretty=format:%H (%ai by %cn)'])
        release = 'dev'
    else:
        assert 'RELEASE' in os.environ
        release = os.environ['RELEASE']
        assert 'dev' != release
        source = None
    if release not in projects:
        raise ValueError('Unknown release type %s' % release)
    project = projects[release]
    aws = project['aws']
    pinger = project['pinger']
    google = project['google']
    hockeyapp = project[options.architecture]['hockeyapp']

    # Get the pinger pinned root cert
    with open(os.path.join('..', 'Resources', pinger['root_cert'])) as f:
        pinger_cert = f.read()

    build_info.add('Version', version)
    build_info.add('BuildNumber', build_number)
    build_info.add('Time', datetime.now().strftime('%m/%d/%Y %H:%M:%S'))
    build_info.add('User', get_username())
    if source is not None:
        build_info.add('Source', '')
    else:
        build_info.add('Source', source)
    build_info.add('HockeyAppAppId', hockeyapp['app_id'])
    build_info.add('AwsPrefix', aws['prefix'])
    build_info.add('AwsAccountId', aws['account_id'])
    build_info.add('AwsIdentityPoolId', aws['identity_pool_id'])
    build_info.add('AwsUnauthRoleArn', aws['unauth_role_arn'])
    build_info.add('AwsAuthRoleArn', aws['auth_role_arn'])
    build_info.add('PingerHostname', pinger['hostname'])
    build_info.add('PingerCertPem', pinger_cert)
    build_info.add('GoogleClientId', google['client_id'])
    build_info.add('GoogleClientSecret', google['client_secret'])
    build_info.add('S3Bucket', aws['s3_bucket'])
    build_info.add('SupportS3Bucket', aws['support_s3_bucket'])
    if options.architecture == 'android':
        build_info.add('FileProvider', project[options.architecture]['fileprovider'])
    build_info.write(path)


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
    parser.add_argument('--architecture', help='Target architecture', default=None, choices=('ios', 'android'))
    options = parser.parse_args()

    if not options.csproj_file or not options.root or not options.architecture:
        print "ERROR: Missing arguments\n%s" % parser.format_help()
        exit(1)

    create_buildinfo(options)

if __name__ == '__main__':
    main()
