#!/usr/bin/env python
import os
import sys
import re

# It would be much easier to use plistlib. However, the Info.plist generated
# by Xamarin studio is radically different from the one generated by
# plistlib. So, the diff of the original and modified Info.plist looks
# horrible. Instead, we use a simple regex script to modify only the
# 3 selected lines.


class PlistFile:
    def __init__(self, path):
        self.path = path
        with open(self.path, 'r') as f:
            self.lines = f.readlines()

    def replace(self, key, value):
        for n in range(len(self.lines)):
            if re.search(key, self.lines[n]):
                break
        else:
            raise KeyError()
        match = re.search('<string>(?P<value>.+)</string>', self.lines[n+1])
        assert match
        self.lines[n+1] = re.sub(match.group('value'), value, self.lines[n+1])

    def write(self):
        with open(self.path + '.new', 'w') as f:
            f.writelines(self.lines)


def main():
    if 'VERSION' in os.environ:
        assert 'BUILD' in os.environ
        version = os.environ['VERSION']
        build = os.environ['BUILD']
        app_id = 'com.nachocove.nachomail.beta'
    else:
        version = '0.1'
        build = '0'
        app_id = 'com.nachocove.nachomail'

    info_plist = PlistFile(sys.argv[1])
    info_plist.replace('CFBundleIdentifier', app_id)
    info_plist.replace('CFBundleVersion', build)
    info_plist.replace('CFBundleShortVersionString', version)
    info_plist.write()

if __name__ == '__main__':
    main()