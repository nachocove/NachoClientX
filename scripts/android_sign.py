#!/usr/bin/env python
# Copyright 2014, NachoCove, Inc
import StringIO
import argparse
import os
import subprocess
import sys
import tempfile
import keyring
from projects import projects


def keyring_username(release):
    return 'nachoclient_android_%s' % release


def sign_apk(options):
    password = keyring.get_password('system', keyring_username(options.release))
    if not password:
        print 'ERROR: could not get password for %s from keychain' % keyring_username(options.release)
        sys.exit(1)

    project = projects[options.release]['android']
    keystore_info = project['keystore']
    if not keystore_info.get('filename', None):
        print "ERROR: no keystore filename for %s in projects" % options.release
        sys.exit(1)
    if not keystore_info.get('alias', None):
        print "ERROR: no keystore alias for %s in projects" % options.release
        sys.exit(1)

    keystore_fullpath = os.path.join(options.keystore_path, keystore_info['filename'])
    if not os.path.exists(keystore_fullpath):
        print "ERROR: keystore does not exist: %s" % keystore_fullpath
        sys.exit(1)

    password_io = StringIO.StringIO(password)

    temp_apk = tempfile.NamedTemporaryFile(suffix=".apk", delete=True)
    android_home = os.environ.get('XAM_ANDROID_HOME',
                                  "%s/Library/Developer/Xamarin/android-sdk-mac_x86" % os.environ['HOME'])
    android_tool_version = os.environ.get('XAM_ANDROID_TOOL_VERSION', '23.0.1')
    zipalign = os.path.join(android_home, 'build-tools', android_tool_version, 'zipalign')
    jarsigner = "/usr/bin/jarsigner"

    jarsigner_args = {'jarsigner': jarsigner,
                      'sigalg': keystore_info.get('sigalg', "SHA1withRSA"),
                      'digestalg': keystore_info.get('digestalg', "SHA1"),
                      'keystore': keystore_fullpath,
                      'signedjar': temp_apk.name,
                      'inputapk': options.inputapk,
                      'keystore_alias': keystore_info['alias'],
                      }

    jarsigner_cmd = "%(jarsigner)s -sigalg %(sigalg)s -digestalg %(digestalg)s -keystore %(keystore)s -signedjar %(signedjar)s %(inputapk)s %(keystore_alias)s" % jarsigner_args
    jarsigner_process = subprocess.Popen(jarsigner_cmd.split(' '), stdin=subprocess.PIPE, stderr=subprocess.PIPE, stdout=subprocess.PIPE)
    (error, output) = jarsigner_process.communicate(password)
    if jarsigner_process.returncode != 0:
        print "ERROR: Could not run jarsigner: \n%s" % error
        sys.exit(jarsigner_process.returncode)

    if error:
        print error

    zipalign_args = {'zipalign': zipalign,
                     'signedapk': temp_apk.name,
                     'finalapk': options.outputapk,
                     }
    zipalign_cmd = "%(zipalign)s -f 4 %(signedapk)s %(finalapk)s" % zipalign_args

    zipalign_process = subprocess.Popen(zipalign_cmd.split(' '))
    (error, output) = zipalign_process.communicate()
    if zipalign_process.returncode != 0:
        print "ERROR: Could not run zipalign:\n%s" % error
        sys.exit(zipalign_process.returncode)
    if error:
        print error
    print "Successfully signed apk %s -> %s" % (options.inputapk, options.outputapk)


def set_password(options):
    keyring.set_password('system', keyring_username(options.release), options.password)
    print "Successfully set password for %s" % keyring_username(options.release)
    sys.exit(0)


def main():
    default_keystore_path = "%s/.keystores" % os.environ['HOME']
    parser = argparse.ArgumentParser()
    subparsers = parser.add_subparsers()
    set_parser = subparsers.add_parser('set-password')
    sign_parser = subparsers.add_parser('sign')

    set_parser.add_argument('password', nargs='?', help='set the password for the alias in the keychain', default=None)
    set_parser.add_argument('--release', '-r', help="Release", choices=('dev', 'alpha', 'beta', 'appstore'),
                            default=None)
    set_parser.set_defaults(func=set_password)

    sign_parser.add_argument('--release', '-r', help="Release", choices=('dev', 'alpha', 'beta', 'appstore'),
                             default=None)
    sign_parser.add_argument('--keystore-path', '-k',
                             help="Where to find the keystore. Default=%s" % default_keystore_path,
                             default=default_keystore_path)
    sign_parser.add_argument('inputapk', help="Input APK", default=None)
    sign_parser.add_argument('outputapk', help="Output APK", default=None)
    sign_parser.set_defaults(func=sign_apk)

    options = parser.parse_args()

    if not options.release:
        options.print_help()
        sys.exit(1)

    if options.release not in projects:
        print "ERROR: unknown release type %s" % options.release
        sys.exit(1)

    options.func(options)


if __name__ == '__main__':
    main()
