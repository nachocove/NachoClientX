#!/usr/bin/env python
import os
import plistlib
import argparse
import subprocess
import hockeyapp


class HockeyappUpload:
    def __init__(self, api_token, app_id):
        self.api_token = api_token
        self.app_id = app_id
        self.hockeyapp_obj = hockeyapp.HockeyApp(api_token=api_token)
        self.app_obj = hockeyapp.App(hockeyapp_obj=self.hockeyapp_obj,
                                     app_id=app_id)

    def upload(self, target_dir, note=None):
        raise NotImplementedError()


class HockeyappUploadIos(HockeyappUpload):
    def __init__(self, api_token, app_id):
        HockeyappUpload.__init__(self, api_token, app_id)
        self.app_folder = None
        self.dsym_folder = None
        self.version_obj = None
        # CFBundleShortVersionString -> HockeyApp "version"
        self.short_version = None
        # CFBundleVersion -> HockeyApp "build"
        self.version = None

    def upload(self, target_dir, note=None):
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

        self.find_version()

        print '\nUploading zipped dSYM file...'
        self.version_obj.update(zip_file, note)

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

    def find_version(self):
        self.version_obj = self.app_obj.find_version(version=self.version, short_version=self.short_version)
        if self.version_obj is None:
            print 'Creating version %s %s' % (self.short_version, self.version)
            self.version_obj = hockeyapp.Version(self.app_obj,
                                                 version=self.version,
                                                 short_version=self.short_version)
            self.version_obj.create()
        assert self.version_obj.version_id is not None


def main():
    parser = argparse.ArgumentParser()
    platform = parser.add_mutually_exclusive_group()
    platform.add_argument('--ios', action='store_true', help='upload iOS dSYM file')
    platform.add_argument('--android', action='store_true', help='upload Android mapping file [not implemented yet]')
    parser.add_argument('target_dir', nargs='?', help='Xamarin target directory')

    options = parser.parse_args()

    if options.target_dir is None:
        parser.print_help()
        exit(0)

    if options.ios:
        hockey_app = HockeyappUploadIos(api_token='92c7e2b0e98642f3b6ad1e3f6403924c',
                                        app_id='5f7134267a5c73933420a1f0efbdfcbf')
    else:
        raise NotImplementedError('Android is not yet supported')
    hockey_app.upload(options.target_dir)


if __name__ == '__main__':
    main()