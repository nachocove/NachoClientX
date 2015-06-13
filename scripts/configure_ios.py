#!/usr/bin/env python
import os
import sys
import plistlib
import shutil
from projects import projects


class PlistFile:
    def __init__(self, path):
        self.path = path
        self.plist = plistlib.readPlist(path)

    def replace(self, key, value):
        """
        Replace the value of an existing key. KeyError exception is raised if the key does not exist.
        """
        if not key in self.plist:
            raise KeyError(key)
        self.plist[key] = value

    def append(self, key, value):
        """
        Add a new value to a key. The key must be a list or a TypeError is raised.
        """
        if not key in self.plist:
            self.plist[key] = [value]
        else:
            if not isinstance(self.plist[key], list):
                raise TypeError(key)
            if value not in self.plist[key]:
                self.plist[key].append(value)

    def write(self, path=None):
        if path is None:
            path = self.path
        plistlib.writePlist(self.plist, path)


def main():
    if 'RELEASE' in os.environ:
        assert 'BUILD' in os.environ and 'VERSION' in os.environ
        version = os.environ['VERSION']
        build = os.environ['BUILD']
        release = os.environ['RELEASE']
    else:
        print 'Development build'
        version = '0.1'
        build = '0'
        release = 'dev'
    if release not in projects:
        raise ValueError('Unknown release type %s' % release)
    app_id = projects[release]['ios']['bundle_id']
    icon_script = projects[release]['ios'].get('icon_script', None)
    display_name = projects[release]['ios']['display_name']

    project_dir = os.path.dirname(os.path.abspath(sys.argv[1]))
    if icon_script is None:
        release_dir = None
    else:
        release_dir = os.path.dirname(icon_script)

    print 'CFBundlerIdentifier = %s' % app_id
    print 'CFBundleShortVersionString = %s' % version
    print 'CFBundleVersion = %s' % build
    print 'CFBundleDisplayName = %s' % display_name

    info_plist = PlistFile(sys.argv[1])
    info_plist.write(os.path.join(project_dir, 'Info.plist.rewritten'))

    info_plist.replace('CFBundleIdentifier', app_id)
    info_plist.replace('CFBundleVersion', build)
    info_plist.replace('CFBundleShortVersionString', version)
    info_plist.replace('CFBundleDisplayName', display_name)
    if release_dir is not None:
        src_path = os.path.join(release_dir, 'GoogleService-Info.plist')
        dst_path = os.path.join(project_dir, 'GoogleService-Info.plist')
        shutil.copyfile(src_path, dst_path)
    google_plist_path = os.path.join(project_dir, 'GoogleService-Info.plist')
    google_plist = plistlib.readPlist(google_plist_path)
    info_plist.append('CFBundleURLSchemes', google_plist['REVERSED_CLIENT_ID'])
    info_plist.write()

    if icon_script is not None:
        print 'Icon script = %s' % icon_script
        script = os.path.basename(icon_script)
        path = '%s/Resources/%s' % (project_dir, release_dir)
        if os.system('sh -c "cd %s; sh %s"' % (path, script)) != 0:
            print 'ERROR: fail to copy icons'
            exit(1)

if __name__ == '__main__':
    main()