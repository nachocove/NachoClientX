import os
import sys
import plistlib


def copy_attributes(app_folder, dsym_folder):
    # Read Info.plist
    app_info_plist_path = os.path.join(app_folder, 'Info.plist')
    dsym_info_plist_path = os.path.join(dsym_folder, 'Contents', 'Info.plist')

    app_info_plist = plistlib.readPlist(app_info_plist_path)
    dsym_info_plist = plistlib.readPlist(dsym_info_plist_path)

    # Copy values over
    def copy_attribute(attr_name):
        if attr_name not in app_info_plist:
            print 'WARN: no %s' % attr_name
        else:
            value = app_info_plist[attr_name]
            dsym_info_plist[attr_name] = value
            print '%s: %s' % (attr_name, value)

    copy_attribute('CFBundleVersion')
    copy_attribute('CFBundleShortVersionString')
    plistlib.writePlist(dsym_info_plist, dsym_info_plist_path)


def main():
    target_dir = sys.argv[1]
    app_folder = os.path.join(target_dir, 'NachoClientiOS.app')
    dsym_folder = os.path.join(target_dir, 'NachoClientiOS.app.dSYM')

    copy_attributes(app_folder, dsym_folder)

    # Zip up dSYM directory
    zip_file = os.path.join(target_dir, 'NachoClientiOS.dSYM.zip')
    os.system('zip -r %s %s' % (zip_file, dsym_folder))

    # Upload to HockeyApp
    puck_path = '../../../HockeyApp/HockeyApp.app/Contents/Resources/puck '
    params = {'-dsym_path': 'NachoClientiOS.dSYM.zip',
              '-upload': 'symbols',
              '-submit': 'auto',
              '-app_id': '5f7134267a5c73933420a1f0efbdfcbf',
              '-api_token': '92c7e2b0e98642f3b6ad1e3f6403924c'}
    params_str = ' '.join(['%s=%s' % (x, y) for (x, y) in params.items()])
    print 'Upload options:', params_str
    ipa_path = os.path.join(target_dir, 'NachoClientiOS-1.ipa')

    os.system('%s %s %s' % (puck_path, params_str, ipa_path))


if __name__ == '__main__':
    main()