#!/usr/bin/env python
import sys
import os
import configure_base
import xml.etree.ElementTree as ET


def edit_manifest(manifest_file, android, build, version):
    ET.register_namespace('android', 'http://schemas.android.com/apk/res/android')
    tree = ET.parse(manifest_file)
    root = tree.getroot()
    root.attrib["package"] = android['package_name']
    root.attrib["{http://schemas.android.com/apk/res/android}versionCode"] = build
    root.attrib["{http://schemas.android.com/apk/res/android}versionName"] = version
    application = root.findall('application')[0]
    application.attrib["{http://schemas.android.com/apk/res/android}label"] = android['label']
    tree.write(manifest_file)


def main():
    (android, release, version, build, release_dir) = configure_base.setup('android')
    edit_manifest(sys.argv[1], android, build, version)
    project_dir = os.getcwd()
    configure_base.copy_icons(android.get('icon_script', None), project_dir, release_dir)

if __name__ == '__main__':
    main()