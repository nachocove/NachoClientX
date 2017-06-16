#!/usr/bin/env python
import argparse
import command
import git
import plistlib
import os
import os.path
import time
import datetime


KINDS = ('store', 'alpha', 'beta')
PLATFORMS = ('ios', 'android')


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('kind', help="The type of build.  e.g., store, alpha, beta")
    parser.add_argument('version', help="The version string.  e.g., 1.2.3")
    parser.add_argument('build', help="The build number.  e.g., 456")
    parser.add_argument('--config', help="Use a custom config plist rather than the one auto chosen by the kind argument")
    parser.add_argument('--ios-only', action='store_true', help="Only build iOS and nothing else")
    parser.add_argument('--android-only', action='store_true', help="Only build Android and nothing else")
    parser.add_argument('--unsigned', action='store_true', help="Only build the unsigned Android .apk")
    parser.add_argument('--no-git', action='store_true', help="Don't do any git branching or tagging (FOR TESTING ONLY)")

    args = parser.parse_args()

    build = Build(args.kind, args.version, args.build)
    platforms = platforms_from_args(args)

    builder = Builder(build, platforms=platforms, config_file=args.config, unsigned_only=args.unsigned, skip_git=args.no_git)
    builder.execute()


def platforms_from_args(args):
    if args.ios_only:
        return ('ios',)
    if args.android_only:
        return ('android',)
    return PLATFORMS


class Build(object):

    kind = None
    major = 0
    minor = 0
    bugfix = 0
    number = 0
    source = None

    def __init__(self, kind, version_string, build_number):
        self.kind = kind
        parts = version_string.split('.')
        self.major = int(parts[0])
        if len(parts) > 1:
            self.minor = int(parts[1])
            if len(parts) > 2:
                self.bufix = int(parts[2])
        self.number = build_number

    @property
    def version_string(self):
        return '%d.%d.%d' % (self.major, self.minor, self.bugfix)

    @property
    def full_branch(self):
        return "v%d.%d.%d" % (self.major, self.minor, self.bugfix)

    @property
    def short_branch(self):
        return "v%d.%d" % (self.major, self.minor)

    @property
    def tag(self):
        if self.kind == 'store':
            return 'v%s_%s' % (self.version_string, self.number)
        return 'v%s_%s_%s' % (self.version_string, self.number, self.kind)


class DevBuild(Build):

    version_string = None
    full_branch = None
    short_branch = None
    tag = None

    def __init__(self):
        self.kind = 'dev'
        self.number = str(int(time.time()))
        self.version_string = 'DEV[%s]' % os.getlogin()


class DictWrappedObject(object):

    _defaults = None
    _values = None

    def __init__(self, values, defaults):
        self._values = values
        self._defaults = defaults

    def __getattr__(self, name):
        if name in self._values:
            value = self._values[name]
        elif name in self._defaults:
            value = self._defaults[name]
        else:
            return super(DictWrappedObject, self).__getattr__(name)
        if isinstance(value, dict):
            self._values[name] = DictWrappedObject(value, self._defaults.get(name, dict()))
            return self._values[name]
        return value

    def __setattr__(self, name, value):
        if hasattr(self, name):
            super(DictWrappedObject, self).__setattr__(name, value)
        else:
            self._values[name] = value

    def __get__(self, name):
        return self._values[name]

    def __set__(self, name, value):
        self._values[name] = value


class BuildConfig(DictWrappedObject):

    DEFAULTS = dict(
        IsDevelopment=False,
        iOS=dict(
            DisplayName=None,
            BundleId=None,
            IconSet='Resources/Images.xcassets/AppIcon.appiconset',
            AppGroup=None,
            iCloudContainer=None,
            ShareBundleId=None,
            HockeyAppAppId=None,
            FileSharingEnabled=False
        ),
        Android=dict(
            PackageName=None,
            IconDrawable='Icon',
            AppNameString='app_name',
            FileProvider=None,
            BackupAPIKey=None,
            HockeyAppAppId=None,
            SigningKeystore=None,
            SigningKeystoreAlias=None
        ),
        AWS=dict(
            Prefix=None,
            AccountId=None,
            IdentityPoolId=None,
            UnauthRoleArn=None,
            AuthRoleArn=None,
            S3Bucket=None,
            SupportS3Bucket=None
        ),
        Pinger=dict(
            Hostname=None,
            RootCert=None,
            CrlSigningCerts=[]
        )
    )

    def __init__(self, path):
        values = plistlib.readPlist(path)
        if values is None:
            raise ArgumentException("No valid plist found at %s" % path)
        super(BuildConfig, self).__init__(values, self.DEFAULTS)


class BuildInfoFile:
    def __init__(self):
        self.entries = []

    def populate_with_config(self, build, config):
        self.add('Version', build.version_string)
        self.add('BuildNumber', build.number)
        self.add('Time', datetime.datetime.now().strftime('%m/%d/%Y %H:%M:%S'))
        self.add('User', os.getlogin())
        self.add('Source', build.source if config.IsDevelopment and build.source is not None else '')

        # Pinger
        self.add('PingerHostname', config.Pinger.Hostname)
        self.add('PingerCertPem', config.Pinger.RootCert)
        self.add('PingerCrlSigningCerts', config.Pinger.CrlSigningCerts)

        # AWS
        self.add('AwsPrefix', config.AWS.Prefix)
        self.add('AwsAccountId', config.AWS.AccountId)
        self.add('AwsIdentityPoolId', config.AWS.IdentityPoolId)
        self.add('AwsUnauthRoleArn', config.AWS.UnauthRoleArn)
        self.add('AwsAuthRoleArn', config.AWS.AuthRoleArn)
        self.add('S3Bucket', config.AWS.S3Bucket)
        self.add('SupportS3Bucket', config.AWS.SupportS3Bucket)

    def write(self, path):
        """
        Generate BuildInfo.cs
        """
        with open(path, 'w') as f:
            print >>f, '// This file is automatically generated every build by Nacho build tools. DO NOT EDIT.'
            print >>f, 'namespace NachoClient.Build'
            print >>f, '{'
            print >>f, '    public class BuildInfo'
            print >>f, '    {'
            for (key, value) in self.entries:
                if isinstance(value, basestring):
                    print >>f, '        public const string %s = @"%s";' % (key, value)
                elif isinstance(value, list):
                    print >>f, '        public static string[] %s = {' % key
                    for s in value:
                        print >>f, '             @"%s",' % s
                    print >>f, '        };'
                elif isinstance(value, self.Code):
                    print >>f, '        %s' % value.body
                else:
                    raise Exception("Unknown type value %s=%s" % (key, value))
            print >>f, '    }'
            print >>f, '}'

    def add(self, key, value):
        self.entries.append((key, value))

    def add_code(self, body):
        self.entries.append((None, self.Code(body)))

    class Code(object):
        def __init__(self, body):
            self.body = body


class Builder(object):

    build = None
    platforms = None
    outputs = None
    config_file = None
    config = None
    unsigned_only = False
    skip_git = False

    def __init__(self, build, platforms, config_file=None, unsigned_only=False, skip_git=False):
        self.build = build
        self.platforms = platforms
        self.config_file = config_file
        self.unsigned_only = unsigned_only
        self.skip_git = skip_git
        self.build.source = git.source_line(cwd=self.nacho_path())
        self.load_config()
        self.outputs = []

    def nacho_path(self, *components):
        root = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
        return os.path.join(root, *components)

    def load_config(self):
        if self.config_file is None:
            self.config_file = self.nacho_path('buildconfig', '%s.plist' % self.build.kind)
        self.config = BuildConfig(self.config_file)

    def execute(self):
        import repos
        if not self.skip_git:
            self.checkout()
        if 'ios' in self.platforms:
            self.build_ios()
        if 'android' in self.platforms:
            self.build_android()
        if not self.skip_git:
            self.tag()
        if len(self.outputs) > 0:
            print ""
            printer = repos.TablePrinter()
            printer.print_table(self.outputs)

    def checkout(self):
        # First, make sure this repo is checked out to the appropriate branch
        # Usually, the branch will be something like v3.5, containing just the major and minor version parts,
        # with each bugfix release built & tagged on that branch.  In rare cases where mutliple bugfix releases
        # are being worked on simultaneously, a v3.5.x branch can be cut, and this checkout will prefer it
        # to the v3.5 branch.
        # If neither the vX.Y or the vX.Y.Z branch is found, a vX.Y branch will be created automatiacally
        # across all repos.  This is handy because it cust the branch when doing the first alpha build all
        # in one step: build.py alpha 3.5.0 123
        import repos
        client_path = self.nacho_path()
        client_repo = repos.Repo(client_path)
        branch = self.build.short_branch
        if client_repo.has_branch(self.build.full_branch):
            branch = self.build.full_branch
        print "Getting branch %s..." % branch
        if not client_repo.has_branch(branch):
            print "Creating branch %s..." % branch
            all_repos = repos.all_repos()
            for repo in all_repos:
                repo.create_branch(branch)
        client_repo.checkout(branch)

        # Next, reload supporting modules in case any have changed as a result of the checkout
        # The most likely change is repos.REPO_NAMES
        reload(command)
        reload(git)
        reload(repos)

        # Finally, checkout all other repos to the matching branch
        all_repos = repos.all_repos()
        for repo in all_repos:
            repo.checkout(branch)

    def tag(self):
        import repos
        print "Tagging as %s..." % self.build.tag
        all_repos = repos.all_repos()
        for repo in all_repos:
            repo.create_tag(self.build.tag)
        self.outputs.append(('git tag:', self.build.tag))

    def build_ios(self):
        print "Building iOS..."
        builder = IOSBuilder(self.nacho_path('NachoClient.sln'), 'NachoClient.iOS', self.build, self.config)
        builder.execute()
        self.outputs.append(('iOS .xarchive:', builder.archive_path))
        self.outputs.append(('iOS .ipa:', builder.ipa_path))

    def build_android(self):
        print "Building Android..."
        builder = AndroidBuilder(self.nacho_path('NachoClient.sln'), 'NachoClient.Android', self.build, self.config, self.unsigned_only)
        builder.execute()
        self.outputs.append(('Android unsigned .apk:', builder.unsigned_apk))
        if not self.unsigned_only:
            self.outputs.append(('Android signed .apk:', builder.signed_apk))


class IOSBuilder(object):

    build = None
    config = None
    solution_path = None
    project_name = None
    archive_path = None
    ipa_path = None

    def __init__(self, solution_path, project_name, build, config):
        self.solution_path = solution_path
        self.project_name = project_name
        self.build = build
        self.config = config

    def project_path(self, *components):
        root = os.path.dirname(self.solution_path)
        return os.path.join(root, self.project_name, *components)

    def execute(self):
        self.configure()
        self.archive()
        self.export()

    def configure(self):
        self.edit_buildinfo()
        self.edit_info()
        self.edit_entitlements()

    def edit_buildinfo(self):
        path = self.project_path('BuildInfo.cs')
        infofile = BuildInfoFile()
        infofile.populate_with_config(self.build, self.config)
        infofile.add('HockeyAppAppId', self.config.iOS.HockeyAppId)
        infofile.add('AppGroup', self.config.iOS.AppGroup)
        infofile.write(path)

    def edit_info(self):
        info_path = self.project_path('Info.plist')
        info = plistlib.readPlist(info_path)
        orig_bundle_id = info['CFBundleIdentifier']
        info['CFBundleIdentifier'] = self.config.iOS.BundleId
        info['CFBundleVersion'] = self.build.number
        info['CFBundleShortVersionString'] = self.build.version_string
        info['CFBundleDisplayName'] = self.config.iOS.DisplayName
        info['CFBundleName'] = self.config.iOS.DisplayName
        info['UIFileSharingEnabled'] = self.config.iOS.FileSharingEnabled
        info['XSAppIconAssets'] = self.config.iOS.IconSet
        for entry in info['CFBundleURLTypes']:
            for i in range(len(entry['CFBundleURLSchemes'])):
                url = entry['CFBundleURLSchemes'][i]
                if url == orig_bundle_id:
                    entry['CFBundleURLSchemes'][i] = self.config.iOS.BundleId
        plistlib.writePlist(info, info_path)

    def edit_entitlements(self):
        entitlements_path = self.project_path('Entitlements.plist')
        entitlements = plistlib.readPlist(entitlements_path)
        entitlements['com.apple.developer.icloud-container-identifiers'] = [self.config.iOS.iCloudContainer]
        # FIXME: re-enable app groups when we get the share extension working
        # entitlements['com.apple.security.application-groups'] = [self.config.iOS.AppGroup]
        plistlib.writePlist(entitlements, entitlements_path)

    def archive(self):
        cmd = command.Command('msbuild', '/t:%s' % self.project_name.replace('.', '_'), '/p:Configuration=Release', '/p:Platform=iPhone', '/p:ArchiveOnBuild=true', self.solution_path)
        cmd.execute()
        # FIXME: need to get the output path and set self.archive_path
        # search ~/Library/Developer/Archives/YYYY-MM-DD for latest 

    def export(self):
        # TODO: use xcodebuild
        pass


class AndroidBuilder(object):

    build = None
    config = None
    solution_path = None
    project_name = None
    unsigned_apk = None
    signed_apk = None
    unsigned_only = None

    def __init__(self, solution_path, project_name, build, config, unsigned_only=False):
        self.solution_path = solution_path
        self.project_name = project_name
        self.build = build
        self.config = config
        self.unsigned_only = unsigned_only

    def project_path(self, *components):
        root = os.path.dirname(self.solution_path)
        return os.path.join(root, self.project_name, *components)

    def execute(self):
        self.configure()
        self.package()
        if not self.unsigned_only:
            self.sign()

    def configure(self):
        self.edit_buildinfo()
        self.edit_manifest()

    def edit_buildinfo(self):
        path = self.project_path('BuildInfo.cs')
        infofile = BuildInfoFile()
        infofile.populate_with_config(self.build, self.config)
        infofile.add('FileProvider', self.config.Android.FileProvider)
        infofile.add('AppNameString', "@string/%s" % self.config.Android.AppNameString)
        infofile.add('IconDrawable', "@drawable/%s" % self.config.Android.IconDrawable)
        infofile.add_code("public static int IconResource { get { return NachoClient.AndroidClient.Resource.Drawable.%s; } }" % self.config.Android.IconDrawable)
        infofile.add_code("public static int AppNameResource { get { return NachoClient.AndroidClient.Resource.String.%s; } }" % self.config.Android.AppNameString)
        infofile.write(path)

    def edit_manifest(self):
        import xml.etree.ElementTree as ET
        ET.register_namespace('android', 'http://schemas.android.com/apk/res/android')
        manifest_path = self.project_path("Properties", "AndroidManifest.xml")
        tree = ET.parse(manifest_path)
        root = tree.getroot()
        root.attrib["package"] = self.config.Android.PackageName
        root.attrib["{http://schemas.android.com/apk/res/android}versionCode"] = self.build.number
        root.attrib["{http://schemas.android.com/apk/res/android}versionName"] = self.build.version_string
        application = root.findall('application')[0]
        application.attrib["{http://schemas.android.com/apk/res/android}label"] = "@string/%s" % self.config.Android.AppNameString
        application.attrib["{http://schemas.android.com/apk/res/android}icon"] = "@drawable/%s" % self.config.Android.IconDrawable
        meta_data = application.findall('meta-data')
        for md in meta_data:
            if md.attrib.get('{http://schemas.android.com/apk/res/android}name', '') == 'com.google.android.backup.api_key':
                md.attrib['{http://schemas.android.com/apk/res/android}value'] = self.config.Android.BackupAPIKey
        provider_data = application.findall('provider')
        for pd in provider_data:
            if pd.attrib.get('{http://schemas.android.com/apk/res/android}authorities', '') == 'com.nachocove.dev.fileprovider':
                pd.attrib['{http://schemas.android.com/apk/res/android}authorities'] = self.config.Android.FileProvider
        tree.write(manifest_path)

    def package(self):
        cmd = command.Command('msbuild', '/t:%s:BuildApk' % self.project_name.replace('.', '_'), '/p:Configuration=Release', self.solution_path)
        cmd.execute()
        self.unsigned_apk = self.project_path('obj', 'Release', 'android', 'bin', '%s.apk' % self.config.Android.PackageName)
        if not os.path.exists(self.unsigned_apk):
            raise Exception("Unsigned APK not found at expected location: %s" % self.unsigned_apk)

    def sign(self):
        pass


if __name__ == '__main__':
    main()
