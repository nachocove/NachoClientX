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

    def remove_list_index(self, key, index):
        """
        Remove a particular value in a list key specified by the index.
        """
        if not key in self.plist:
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


def find_url_type_by_scheme(plist, scheme):
    index = 0
    bundle_url_types = plist.get('CFBundleURLTypes')
    for url_type in bundle_url_types:
        url_schemes = url_type['CFBundleURLSchemes']
        if scheme in url_schemes:
            return index
        index += 1
    return None


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

    orig_bundle_id = info_plist.get('CFBundleIdentifier')

    info_plist.replace('CFBundleIdentifier', app_id)
    info_plist.replace('CFBundleVersion', build)
    info_plist.replace('CFBundleShortVersionString', version)
    info_plist.replace('CFBundleDisplayName', display_name)

    # Copy the google info plsit over
    google_path = '%s/Resources/%s' % (project_dir, release_dir)
    src_path = os.path.join(google_path, 'GoogleService-Info.plist')
    dst_path = os.path.join(project_dir, 'GoogleService-Info.plist')
    # Need to remove the original value. This is done by reading the dst .plist
    # and remove the value there
    old_google_plist = plistlib.readPlist(dst_path)
    rev_client_id = old_google_plist['REVERSED_CLIENT_ID']
    print 'Remove old google service value %s' % rev_client_id
    index = find_url_type_by_scheme(info_plist, rev_client_id)
    if index is not None:
        info_plist.remove_list_index('CFBundleURLTypes', index)
    if release_dir is not None:
        shutil.copyfile(src_path, dst_path)
    google_plist_path = os.path.join(project_dir, 'GoogleService-Info.plist')
    google_plist = plistlib.readPlist(google_plist_path)
    print 'Add new google service value %s' % google_plist['REVERSED_CLIENT_ID']

    index = find_url_type_by_scheme(info_plist, orig_bundle_id)
    if index is not None:
        info_plist.remove_list_index('CFBundleURLTypes', index)

    # Create a new entry and insert it
    entry = {
        'CFBundleURLName': 'google',
        'CFBundleURLSchemes': [google_plist['REVERSED_CLIENT_ID']],
        'CFBundleURLTypes': 'Editor'
    }
    info_plist.append('CFBundleURLTypes', entry)

    entry2 = {
        'CFBundleURLName': 'google',
        'CFBundleURLSchemes': [app_id],
        'CFBundleURLTypes': 'Editor'
    }
    info_plist.append('CFBundleURLTypes', entry2)

    # Update the entry with bundle ID as well
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
