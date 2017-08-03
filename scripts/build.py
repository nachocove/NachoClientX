#!/usr/bin/env python
import argparse
import command
import git
import plistlib
import os
import os.path
import time
import datetime
import tempfile
import getpass
import shutil


KINDS = ('store', 'alpha', 'beta', 'bluecedar')
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

    builder = Builder(build, platforms=platforms, config_file=args.config, unsigned_only=args.unsigned or args.kind == 'bluecedar', skip_git=args.no_git)
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
        Defines='',
        iOS=dict(
            DisplayName=None,
            BundleId=None,
            IconSet='Resources/Images.xcassets/AppIcon.appiconset',
            AppGroup=None,
            iCloudContainer=None,
            ShareBundleId=None,
            CallerIdBundleId=None,
            HockeyAppAppId=None,
            FileSharingEnabled=False
        ),
        Android=dict(
            PackageName=None,
            IconDrawable='Icon',
            RoundIconDrawable='IconRounded',
            AppNameString='app_name',
            FileProvider=None,
            BackupAPIKey=None,
            HockeyAppAppId=None,
            SigningKeystore=None
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
    output_path = None

    def __init__(self, build, platforms, config_file=None, unsigned_only=False, skip_git=False):
        self.build = build
        self.platforms = platforms
        self.config_file = config_file
        self.unsigned_only = unsigned_only
        self.skip_git = skip_git
        self.build.source = git.source_line(cwd=self.nacho_path())
        self.output_path = self.nacho_path('bin', 'Nacho-%s' % self.build.tag)
        if os.path.exists(self.output_path):
            shutil.rmtree(self.output_path)
        os.makedirs(self.output_path)
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
                repo.push()
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
            repo.push(self.build.tag)
        self.outputs.append(('git tag:', self.build.tag))

    def build_ios(self):
        print "Building iOS..."
        builder = IOSBuilder(self.nacho_path('NachoClient.sln'), 'NachoClient.iOS', self.build, self.config, output_path=self.output_path)
        builder.execute()
        self.outputs.append(('iOS .xarchive:', builder.archive_path))
        self.outputs.append(('iOS .ipa:', builder.ipa_path))

    def build_android(self):
        print "Building Android..."
        builder = AndroidBuilder(self.nacho_path('NachoClient.sln'), 'NachoClient.Android', self.build, self.config, self.unsigned_only, output_path=self.output_path)
        builder.execute()
        self.outputs.append(('Android unsigned .apk:', builder.unsigned_apk))
        if not self.unsigned_only:
            self.outputs.append(('Android signed .apk:', builder.signed_apk))


class IOSBuilder(object):

    build = None
    config = None
    solution_path = None
    project_name = None
    callerid_project_name = 'NachoClientCallerID.iOS'
    share_project_name = 'NachoClientShare.iOS'
    archive_path = None
    ipa_path = None
    output_path = None

    def __init__(self, solution_path, project_name, build, config, output_path=None):
        self.solution_path = solution_path
        self.project_name = project_name
        self.build = build
        self.config = config
        self.output_path = output_path

    def project_path(self, *components):
        root = os.path.dirname(self.solution_path)
        return os.path.join(root, self.project_name, *components)

    def callerid_project_path(self, *components):
        root = os.path.dirname(self.solution_path)
        return os.path.join(root, self.callerid_project_name, *components)

    def share_project_path(self, *components):
        root = os.path.dirname(self.solution_path)
        return os.path.join(root, self.share_project_name, *components)

    def execute(self):
        self.configure()
        self.archive()
        self.export()

    def configure(self):
        self.edit_buildinfo()
        self.edit_info()
        self.edit_entitlements()
        self.edit_callerid_buildinfo()
        self.edit_callerid_info()
        self.edit_callerid_entitlements()
        self.edit_share_buildinfo()
        self.edit_share_info()
        self.edit_share_entitlements()

    def edit_buildinfo(self):
        path = self.project_path('BuildInfo.cs')
        infofile = BuildInfoFile()
        infofile.populate_with_config(self.build, self.config)
        infofile.add('HockeyAppAppId', self.config.iOS.HockeyAppId)
        infofile.add('AppGroup', self.config.iOS.AppGroup)
        infofile.add('ShareBundleId', self.config.iOS.ShareBundleId)
        infofile.add('CallerIdBundleId', self.config.iOS.CallerIdBundleId)
        infofile.write(path)

    def edit_callerid_buildinfo(self):
        path = self.callerid_project_path('BuildInfo.cs')
        infofile = BuildInfoFile()
        infofile.add('AppGroup', self.config.iOS.AppGroup)
        infofile.write(path)

    def edit_share_buildinfo(self):
        path = self.share_project_path('BuildInfo.cs')
        infofile = BuildInfoFile()
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

    def edit_callerid_info(self):
        info_path = self.callerid_project_path('Info.plist')
        info = plistlib.readPlist(info_path)
        orig_bundle_id = info['CFBundleIdentifier']
        info['CFBundleIdentifier'] = self.config.iOS.CallerIdBundleId
        info['CFBundleVersion'] = self.build.number
        info['CFBundleShortVersionString'] = self.build.version_string
        info['CFBundleDisplayName'] = self.config.iOS.DisplayName
        info['CFBundleName'] = self.config.iOS.DisplayName
        plistlib.writePlist(info, info_path)

    def edit_share_info(self):
        info_path = self.share_project_path('Info.plist')
        info = plistlib.readPlist(info_path)
        orig_bundle_id = info['CFBundleIdentifier']
        info['CFBundleIdentifier'] = self.config.iOS.ShareBundleId
        info['CFBundleVersion'] = self.build.number
        info['CFBundleShortVersionString'] = self.build.version_string
        info['CFBundleDisplayName'] = self.config.iOS.DisplayName
        info['CFBundleName'] = self.config.iOS.DisplayName
        plistlib.writePlist(info, info_path)

    def edit_entitlements(self):
        entitlements_path = self.project_path('Entitlements.plist')
        entitlements = plistlib.readPlist(entitlements_path)
        entitlements['com.apple.developer.icloud-container-identifiers'] = [self.config.iOS.iCloudContainer]
        entitlements['com.apple.security.application-groups'] = [self.config.iOS.AppGroup]
        plistlib.writePlist(entitlements, entitlements_path)

    def edit_callerid_entitlements(self):
        entitlements_path = self.callerid_project_path('Entitlements.plist')
        entitlements = plistlib.readPlist(entitlements_path)
        entitlements['com.apple.security.application-groups'] = [self.config.iOS.AppGroup]
        plistlib.writePlist(entitlements, entitlements_path)

    def edit_share_entitlements(self):
        entitlements_path = self.share_project_path('Entitlements.plist')
        entitlements = plistlib.readPlist(entitlements_path)
        entitlements['com.apple.security.application-groups'] = [self.config.iOS.AppGroup]
        plistlib.writePlist(entitlements, entitlements_path)

    def archive(self):
        cmd = command.Command('msbuild', '/t:%s' % self.project_name.replace('.', '_'), '/p:Configuration=Release', '/p:Platform=iPhone', '/p:ArchiveOnBuild=true', '/p:CustomDefines=%s' % self.config.Defines, self.solution_path)
        cmd.execute()
        self.archive_path = self.locate_archive_for_buildtime(datetime.datetime.now())

    def locate_archive_for_buildtime(self, buildtime):
        # An xarchive is named according to the build time, with a precision of 1 minute
        # Since we don't know exactly which time msbuild used, we'll search for a few minutes.
        # The risk of finding the wrong build is impossible as long as builds continue to take at least several minutes.
        expected_xarchive_format = "%s %d-%d-%02d %d.%02d %s.xcarchive"
        checked_xarchives = []
        for i in range(3):
            expected_xarchive = os.path.join(os.getenv('HOME'), 'Library', 'Developer', 'Xcode', 'Archives', buildtime.strftime("%Y-%m-%d"), expected_xarchive_format % (self.project_name, buildtime.date().month, buildtime.date().day, buildtime.date().year % 100, buildtime.time().hour % 12 if buildtime.time().hour > 0 else 12, buildtime.time().minute, "PM" if buildtime.time().hour >= 12 else "AM"))
            if os.path.exists(expected_xarchive):
                break
            checked_xarchives.append(expected_xarchive)
            buildtime = buildtime - datetime.timedelta(minutes=1)
        if not os.path.exists(expected_xarchive):
            raise Exception("Expected xarchive does not exist, checked:\n  %s" % "\n  ".join(checked_xarchives))
        return expected_xarchive

    def export(self):
        expected_ipa_path = os.path.join(self.output_path, '%s.ipa' % self.config.iOS.DisplayName)
        plist_path = self.create_export_plist()
        cmd = command.Command('xcodebuild', '-exportArchive', '-archivePath', self.archive_path, '-exportPath', os.path.dirname(expected_ipa_path), '-exportOptionsPlist', plist_path)
        cmd.execute()
        if not os.path.exists(expected_ipa_path):
            raise Exception("Export failed %s" % ' '.join(cmd.cmd))
        final_ipa_path = os.path.join(self.output_path, 'NachoMail-%s.ipa' % self.build.tag)
        os.rename(expected_ipa_path, final_ipa_path)
        self.ipa_path = final_ipa_path

    def create_export_plist(self):
        with tempfile.NamedTemporaryFile(delete=False) as temp_file:
            if self.build.kind in ('store', 'bluecedar'):
                options = dict(
                    method='app-store'
                )
            else:
                options = dict(
                    method='enterprise',
                    compileBitcode=False,
                    iCloudContainerEnvironment='Production'
                )
            plistlib.writePlist(options, temp_file)
            return temp_file.name


class AndroidBuilder(object):

    build = None
    config = None
    solution_path = None
    project_name = None
    unsigned_apk = None
    signed_apk = None
    unsigned_only = None
    output_path = None

    def __init__(self, solution_path, project_name, build, config, unsigned_only=False, output_path=None):
        self.solution_path = solution_path
        self.project_name = project_name
        self.build = build
        self.config = config
        self.unsigned_only = unsigned_only
        self.output_path = output_path

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
        infofile.add('PackageName', self.config.Android.PackageName)
        infofile.add('FileProvider', self.config.Android.FileProvider)
        infofile.add('AppNameString', "@string/%s" % self.config.Android.AppNameString)
        infofile.add('IconDrawable', "@drawable/%s" % self.config.Android.IconDrawable)
        infofile.add('RoundIconDrawable', "@drawable/%s" % self.config.Android.RoundIconDrawable)
        infofile.add_code("public static int IconResource { get { return NachoClient.AndroidClient.Resource.Drawable.%s; } }" % self.config.Android.IconDrawable)
        infofile.add_code("public static int RoundIconResource { get { return NachoClient.AndroidClient.Resource.Drawable.%s; } }" % self.config.Android.RoundIconDrawable)
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
        expected_apk = self.unsigned_apk = self.project_path('obj', 'Release', 'android', 'bin', '%s.apk' % self.config.Android.PackageName)
        if os.path.exists(expected_apk):
            # remove any old apk to ensure that we pick up only a newly created one
            os.unlink(expected_apk)
        cmd = command.Command('msbuild', '/t:%s:BuildApk' % self.project_name.replace('.', '_'), '/p:Configuration=Release', '/p:CustomDefines=%s' % self.config.Defines, self.solution_path)
        cmd.execute()
        if not os.path.exists(expected_apk):
            raise Exception("Unsigned APK not found at expected location: %s" % expected_apk)
        final_apk_path = os.path.join(self.output_path, 'NachoMail-%s.apk' % self.build.tag)
        os.rename(expected_apk, final_apk_path)
        self.unsigned_apk = final_apk_path

    def sign(self):
        keystore = self.config.Android.SigningKeystore
        signed_apk = os.path.join(self.output_path, 'NachoMail-%s-signed.apk' % self.build.tag)
        with tempfile.NamedTemporaryFile(suffix=".apk") as temp_apk:
            build_tools = self.get_build_tools_root()
            cmd = command.Command(os.path.join(build_tools, 'zipalign'), '-f', '-p', '4', self.unsigned_apk, temp_apk.name)
            cmd.execute()
            keystore = os.path.join(os.getenv('HOME'), 'Library', 'Developer', 'Xamarin', 'Keystore', self.config.Android.SigningKeystore, "%s.keystore" % self.config.Android.SigningKeystore)
            cmd = command.Command(os.path.join(build_tools, 'apksigner'), 'sign', '--ks', keystore, '--out', signed_apk, temp_apk.name)
            attempts = 0
            password_success = False
            while attempts < 3 and not password_success:
                password = self.get_keystore_password()
                cmd.stdin = password
                try:
                    cmd.execute()
                    password_success = True
                except command.CommandError:
                    attempts += 1
            if not os.path.exists(signed_apk):
                raise Exception("Signed APK not created")
        self.signed_apk = signed_apk

    def get_build_tools_root(self):
        sdk_root = self.get_sdk_root()
        build_tools_parent = os.path.join(sdk_root, 'build-tools')
        available_tools = sorted(os.listdir(build_tools_parent), key=version_to_number, reverse=True)
        for tools in available_tools:
            if os.path.exists(os.path.join(build_tools_parent, tools, 'aapt')):
                return os.path.join(build_tools_parent, tools)
        return None

    def get_sdk_root(self):
        # FIXME: this is for xbuild, maybe a leftover from xamarin?...does msbuild have an equivalent?
        config_path = os.path.join(os.getenv('HOME'), '.config', 'xbuild', 'monodroid-config.xml')
        import xml.etree.ElementTree as ET
        config_xml = ET.parse(config_path)
        monodroid = config_xml.getroot()
        sdk = monodroid.findall('android-sdk')[0]
        return sdk.attrib['path']

    def get_keystore_password(self):
        has_keyring = False
        try:
            import keyring
            has_keyring = True
        except ImportError:
            pass
        password = None
        key_name = "NachoBuild." + self.config.Android.SigningKeystore
        if has_keyring:
            password = keyring.get_password("system", key_name)
        if password is None:
            password = getpass.getpass("Keystore Password: ")
            if has_keyring:
                keyring.set_password("system", key_name, password)
            else:
                print "To avoid password entry each time you build, run $ pip install keyring"
        return password


def version_to_number(version_string):
    parts = version_string.split('.')
    n = 0
    factor = 1000000
    for part in parts:
        try:
            d = int(part)
        except ValueError:
            return 0
        n += factor * d
        factor /= 100
    return n


if __name__ == '__main__':
    main()
