#!/usr/bin/env python
import os
import sys
import plistlib


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

    info_plist = plistlib.readPlist(sys.argv[1])
    print 'CFBundleIdentifier = %s' % app_id
    print 'CFBundleShortVersionString = %s' % version
    print 'CFBundleVersion = %s' % build
    info_plist['CFBundleVersion'] = build
    info_plist['CFBundleShortVersionString'] = version
    info_plist['CFBundleIdentifier'] = app_id
    plistlib.writePlist(info_plist, sys.argv[1])

if __name__ == '__main__':
    main()