#!/usr/bin/env python
import os
import sys
import plistlib
import shutil
import configure_base


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

    def remove_list_index(self, key, index):
        """
        Remove a particular value in a list key specified by the index.
        """
        if key not in self.plist:
            return
        if not isinstance(self.plist[key], list):
            raise TypeError(key)
        self.plist[key].pop(index)

    def write(self, path=None):
        if path is None:
            path = self.path
        plistlib.writePlist(self.plist, path)

    def get(self, key):
        return self.plist.get(key)


def edit_plist(plist_file, ios, build, version, project_dir, release_dir):
    app_id = ios['bundle_id']
    display_name = ios['display_name']

    print 'CFBundlerIdentifier = %s' % app_id
    print 'CFBundleShortVersionString = %s' % version
    print 'CFBundleVersion = %s' % build
    print 'CFBundleDisplayName = %s' % display_name

    info_plist = PlistFile(plist_file)
    info_plist.write(os.path.join(project_dir, 'Info.plist.rewritten'))

    orig_bundle_id = info_plist.get('CFBundleIdentifier')

    info_plist.replace('CFBundleIdentifier', app_id)
    info_plist.replace('CFBundleVersion', build)
    info_plist.replace('CFBundleShortVersionString', version)
    info_plist.replace('CFBundleDisplayName', display_name)

    # Update the entry with bundle ID as well
    info_plist.write()


def edit_entitlements(entitlements_file, ios, build, version, project_dir, release_dir):
    app_group = ios['app_group']
    entitlements_plist = PlistFile(entitlements_file)
    entitlements_plist.write(os.path.join(project_dir, 'Entitlements.plist.rewritten'))
    entitlements_plist.remove_list_index('com.apple.security.application-groups', 0)
    entitlements_plist.append('com.apple.security.application-groups', app_group)
    entitlements_plist.write()


def main():
    (ios, release, version, build, release_dir) = configure_base.setup('ios_share')
    project_dir = os.path.dirname(os.path.abspath(sys.argv[1]))
    edit_entitlements(os.path.join(project_dir, 'Entitlements.plist'), ios, build, version, project_dir, release_dir)
    edit_plist(sys.argv[1], ios, build, version, project_dir, release_dir)


if __name__ == '__main__':
    main()
