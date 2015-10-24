#!/usr/bin/env python
import os
import sys
import re
import plistlib
import argparse
import subprocess
import hockeyapp
from projects import projects


class HockeyappUpload(object):
    def __init__(self, api_token, app_id):
        self.api_token = api_token
        self.app_id = app_id
        self.hockeyapp_obj = hockeyapp.HockeyApp(api_token=api_token)
        self.app_obj = hockeyapp.App(hockeyapp_obj=self.hockeyapp_obj,
                                     app_id=app_id)

    def upload(self, target_dir, filename, note=None, debug=False):
        raise NotImplementedError()

    def upload_version(self, filename, zipped_dsym_file, version, short_version, note):
        assert version is not None and short_version is not None
        version_obj = self.app_obj.find_version(version=version, short_version=short_version)
        if version_obj is None:
            print 'Creating version %s %s' % (short_version, version)
            version_obj = hockeyapp.Version(self.app_obj,
                                                 version=version,
                                                 short_version=short_version)
            version_obj.create()
        else:
            print 'Updating version %s %s' % (short_version, version)

        assert version_obj.version_id is not None
        version_obj.update(zipped_dsym_file=zipped_dsym_file, ipa_file=filename, note=note)


class HockeyappUploadAndroid(HockeyappUpload):
    def __init__(self, api_token, app_id):
        super(HockeyappUploadAndroid, self).__init__(api_token, app_id)

    def upload(self, target_dir, filename, note=None, debug=False):
        assert filename
        android_home = os.environ.get('XAM_ANDROID_HOME', "%s/Library/Developer/Xamarin/android-sdk-mac_x86" % os.environ['HOME'])
        android_tool_version = os.environ.get('XAM_ANDROID_TOOL_VERSION', '19.1.0')
        aapt = os.path.join(android_home, 'build-tools', android_tool_version, 'aapt')
        if not os.path.exists(aapt):
            raise ValueError("aapt not found: %s" % aapt)

        aapt_process = subprocess.Popen([aapt, "dump", "badging", filename], stdout=subprocess.PIPE, stderr=subprocess.PIPE)
        (output, error) = aapt_process.communicate()
        if aapt_process.returncode:
            raise Exception("ERROR: failed to run aapt: %s\n%s" % (aapt_process.returncode, error))
        if debug:
            print output

        versionName = None
        versionCode = None
        for line in output.split('\n'):
            if line.startswith('package: name='):
                match = re.match("^package: name='(?P<name>.+)' versionCode='(?P<versionCode>\d+)' versionName='(?P<versionName>.+)'", line)
                if not match:
                    raise Exception("No pattern matched for versionCode finding")
                versionName = match.group('versionName')
                versionCode = match.group('versionCode')
                break
        if debug:
            print "Android versionCode=%s versionName=%s" % (versionCode, versionName)
        assert versionName and versionCode
        # see http://support.hockeyapp.net/kb/api/api-versions#create-version
        # bundle_version - mandatory, set to CFBundleVersion (iOS and OS X) or to versionCode (Android)
        # bundle_short_version - optional, set to CFBundleShortVersionString (iOS and OS X) or to versionName (Android)
        self.upload_version(filename, None, version=versionCode, short_version=versionName, note=note)


class HockeyappUploadIos(HockeyappUpload):
    def __init__(self, api_token, app_id):
        super(HockeyappUploadIos, self).__init__(api_token, app_id)
        self.app_folder = None
        self.dsym_folder = None
        # CFBundleShortVersionString -> HockeyApp "version"
        self.short_version = None
        # CFBundleVersion -> HockeyApp "build"
        self.version = None

    def upload(self, target_dir, filename, note=None, debug=False):
        assert target_dir
        self.app_folder = os.path.join(target_dir, 'NachoClientiOS.app')
        self.dsym_folder = os.path.join(target_dir, 'NachoClientiOS.app.dSYM')

        # Print out the UUID
        try:
            output = subprocess.check_output(['dwarfdump', '--uuid', '%s/NachoClientiOS' % self.app_folder])
            print output
        except (OSError, subprocess.CalledProcessError):
            pass  # ok if we cannot extract UUID. maybe dwarfdump is not installed.

        self.copy_attributes()

        # Zip up dSYM directory
        print '\nCreating zipped dSYM file...'
        zip_file = os.path.join(target_dir, 'NachoClientiOS.dSYM.zip')
        os.system('rm -f %s' % zip_file)
        os.system('zip -r %s %s' % (zip_file, self.dsym_folder))

        if filename is None:
            print '\nUploading zipped dSYM file...'
        else:
            print '\nUploading zipped dSYM and ipa files...'
            filename = os.path.join(target_dir, filename)

        self.upload_version(filename, zip_file, self.version, self.short_version, note)

    def copy_attributes(self):
        # Read Info.plist
        app_info_plist_path = os.path.join(self.app_folder, 'Info.plist')
        dsym_info_plist_path = os.path.join(self.dsym_folder, 'Contents', 'Info.plist')

        app_info_plist = plistlib.readPlist(app_info_plist_path)
        dsym_info_plist = plistlib.readPlist(dsym_info_plist_path)

        # Copy values over
        def copy_attribute(attr_name):
            if attr_name not in app_info_plist:
                print 'WARN: no %s' % attr_name
                value = None
            else:
                value = app_info_plist[attr_name]
                dsym_info_plist[attr_name] = value
                print '%s: %s' % (attr_name, value)
            return value

        self.version = copy_attribute('CFBundleVersion')
        self.short_version = copy_attribute('CFBundleShortVersionString')
        plistlib.writePlist(dsym_info_plist, dsym_info_plist_path)


skip_file = '.skip_hockeyapp_upload'

def main():
    parser = argparse.ArgumentParser()
    platform = parser.add_mutually_exclusive_group()
    platform.add_argument('--ios', action='store_true', help='upload iOS dSYM file')
    platform.add_argument('--android', action='store_true', help='upload Android mapping file')
    parser.add_argument('--no-skip', action='store_true', help='Ignore .skip_hockeyapp_upload.')
    parser.add_argument('target_dir', nargs='?', help='Xamarin target directory')
    parser.add_argument('--file', '-f', nargs='?', help='filename', default=None)
    parser.add_argument('--release', nargs='?', choices=('dev', 'alpha', 'beta', 'appstore'), help="Release", default=None)
    parser.add_argument('--debug', '-d', action='store_true', help="Debug", default=False)

    options = parser.parse_args()

    if options.target_dir is None:
        parser.print_help()
        exit(0)

    architecture = None # make static analysis happy
    if options.ios:
        architecture = 'ios'
    elif options.android:
        architecture = 'android'
    else:
        print "ERROR: No architecture flag given"
        parser.print_help()
        exit(0)

    # if .skip_hockeyapp_upload exists, early exit unless --no-skip is used
    # This option is to make sure that we don't forget to upload dsym
    # for official (beta / app store) builds.
    if not options.no_skip and os.path.exists(skip_file):
        print 'Skipping HockeyApp upload.'
        exit(0)

    if options.release is None and 'RELEASE' in os.environ:
        if not 'VERSION' in os.environ or not 'BUILD' in os.environ:
            print "ERROR: No VERSION or BUILD given in env var"
            sys.exit(1)
        release = os.environ['RELEASE']
    elif options.release is not None:
        release = options.release
    else:
        release = 'dev'

    if release not in projects:
        raise ValueError('unknown release type %s' % release)

    project = projects[release]

    hockeyapp_params = project[architecture]['hockeyapp']

    filename = options.file  # can be None in some cases

    if options.ios:
        # For alpha and beta builds, we also upload .ipa. For app store builds,
        # there is no need to upload .ipa because the .ipa is submitted to the
        # official app store.
        if not filename and release in ['alpha', 'beta']:
            if 'BUILD' not in os.environ or 'VERSION' not in os.environ:
                print "ERROR: No build and version in env var. Required for ios uploads"
                sys.exit(1)

            filename = 'NachoClientiOS-%s.ipa' % os.environ['BUILD']
            if not os.path.exists(os.path.join(options.target_dir, filename)):
                # The new format of .ipa file is not found. Try the old format.
                filename = 'NachoClientiOS-%s.ipa' % os.environ['VERSION']
                if not os.path.exists(os.path.join(options.target_dir, filename)):
                    print 'ERROR: Cannot find .ipa file in %s' % options.target_dir
                    sys.exit(1)
        hockey_app_klass = HockeyappUploadIos
    else:
        if not filename:
            filename = '%s.apk' % project[architecture]['package_name']
        if not filename.startswith("/"):
            filename = os.path.join(options.target_dir, filename)
        if not os.path.exists(filename):
            print 'ERROR: Cannot find .apk file: %s' % filename
            sys.exit(1)
        hockey_app_klass = HockeyappUploadAndroid

    print 'Uploading %s to HockeyApp %s' % (filename, hockeyapp_params['app_id'])
    hockey_app = hockey_app_klass(api_token=hockeyapp_params['api_token'],
                                        app_id=hockeyapp_params['app_id'])
    hockey_app.upload(target_dir=options.target_dir, filename=filename, debug=options.debug)


if __name__ == '__main__':
    main()
