#!/usr/bin/env python
# This file lists all build-configurable parameters of Nacho Mail.
# Most of the parameters go into BuildInfo.cs
#
# The following are the dictionary keys:
# 1. Build target - dev, alpha, beta, appstore
# 2. Component - Often a particular endpoints - hockeyapp, aws, pinger. But it can be anything that groups a set of
#                parameters.
import sys

projects = {
    'dev': {
        'ios': {
            'bundle_id': 'com.nachocove.nachomail',
            'display_name': '[dev] Apollo Mail',
            'file_sharing': True,
            'app_group': 'group.com.nachocove.nachomail',
            'hockeyapp': {'app_id': '', 'api_token': ''},
            'icloud_container': 'iCloud.com.nachocove.nachomail'
        },
        'ios_share': {
            'bundle_id': 'com.nachocove.nachomail.share',
            'app_group': 'group.com.nachocove.nachomail',
            'display_name': '[dev] Apollo Mail'
        },
        'mac': {
            'bundle_id': 'com.nachocove.nachomail',
            'display_name': '[dev] Nacho Mail'
        },
        'android': {
            'package_name': 'com.nachocove.nachomail.dev',
            'label': '[dev] Apollo Mail',
            'hockeyapp': {'app_id': '', 'api_token': ''},
            'keystore': {'filename': '',
                         'alias': '',
                         },
            'backup.api_key': '',
            'fileprovider': 'com.nachocove.dev.fileprovider',
        },
        'aws': {
            'prefix': 'dev',
            'account_id': '',
            'identity_pool_id': '',
            'unauth_role_arn': '',
            'auth_role_arn': '',
            's3_bucket': '',
            'support_s3_bucket': '',
        },
        'pinger': {
            'hostname': 'pinger.officetaco.com',
            'root_cert': 'pinger.pem',
            'crl_signing_certs': ['nachocove-crl-cert.pem', 'officetaco-crl-cert.pem'],
        },
        'google': {
            'client_id': '',
            'client_secret': '',
        }
    },
    'alpha': {
        'ios': {
            'bundle_id': 'com.nachocove.nachomail.alpha',
            'display_name': 'Apollo Mail',
            'icon_script': 'alpha/copy.sh',
            'file_sharing': True,
            'app_group': 'group.com.nachocove.nachomail.alpha',
            'hockeyapp': {'app_id': '', 'api_token': ''},
            'icloud_container': 'iCloud.com.nachocove.nachomail.alpha'
        },
        'ios_share': {
            'bundle_id': 'com.nachocove.nachomail.alpha.share',
            'app_group': 'group.com.nachocove.nachomail.alpha',
            'display_name': 'Apollo Mail'
        },
        'android': {
            'package_name': 'com.nachocove.nachomail.alpha',
            'label': 'Apollo Mail',
            'icon_script': 'alpha/copy.sh',
            'hockeyapp': {'app_id': '', 'api_token': ''},
            'keystore': {'filename': 'com.nachocove.mail.alpha.keystore',
                         'alias': 'com.nachocove.mail.alpha',
                         },
            'backup.api_key': '',
            'fileprovider': 'com.nachocove.alpha.fileprovider',
        },
        'aws': {
            'prefix': 'alpha',
            'account_id': '',
            'identity_pool_id': '',
            'unauth_role_arn': '',
            'auth_role_arn': '',
            's3_bucket': 'd3daf3ef-alpha-t3-',
            'support_s3_bucket': 'd3daf3ef-alpha-t3-trouble-tickets',
        },
        'pinger': {
            'hostname': 'alphapinger.officetaco.com',
            'root_cert': 'beta-pinger.pem',
            'crl_signing_cert': [],
        },
        'google': {
            'client_id': '',
            'client_secret': '',
        }
    },
    'beta': {
        'ios': {
            'bundle_id': 'com.nachocove.nachomail.beta',
            'display_name': 'Apollo Mail',
            'icon_script': 'beta/copy.sh',
            'file_sharing': False,
            'app_group': 'group.com.nachocove.nachomail.beta',
            'hockeyapp': {'app_id': '', 'api_token': ''},
            'icloud_container': 'iCloud.com.nachocove.nachomail.beta'
        },
        'ios_share': {
            'bundle_id': 'com.nachocove.nachomail.beta.share',
            'app_group': 'group.com.nachocove.nachomail.beta',
            'display_name': 'Apollo Mail'
        },
        'android': {
            'package_name': 'com.nachocove.nachomail',
            'label': 'Apollo Mail',
            'icon_script': 'beta/copy.sh',
            'hockeyapp': {'app_id': '', 'api_token': ''},
            'keystore': {'filename': 'com.nachocove.mail.keystore',
                         'alias': 'com.nachocove.mail',
                         },
            'backup.api_key': '',
            'fileprovider': 'com.nachocove.fileprovider',
        },
        'aws': {
            'prefix': 'beta',
            'account_id': '',
            'identity_pool_id': '',
            'unauth_role_arn': '',
            'auth_role_arn': '',
            's3_bucket': '3ca28b5e-beta-t3-',
            'support_s3_bucket': '3ca28b5e-beta-t3-trouble-tickets',
        },
        'pinger': {
            'hostname': '',
            'root_cert': 'beta-pinger.pem',
            'crl_signing_cert': [],
        },
        'google': {
            'client_id': '',
            'client_secret': '',
        }
    },
    'appstore': {
        'ios': {
            'bundle_id': 'com.nachocove.mail',
            'display_name': 'Apollo Mail',
            'icon_script': 'appstore/copy.sh',
            'file_sharing': False,
            'app_group': 'group.com.nachocove.mail',
            'hockeyapp': {'app_id': '', 'api_token': ''},
            'icloud_container': 'iCloud.com.nachocove.mail'
        },
        'ios_share': {
            'bundle_id': 'com.nachocove.mail.share',
            'app_group': 'group.com.nachocove.mail',
            'display_name': 'Apollo Mail'
        },
        'android': {
            'package_name': 'com.nachocove.nachomail',
            'label': 'Apollo Mail',
            'icon_script': 'appstore/copy.sh',
            'hockeyapp': {'app_id': '', 'api_token': ''},
            'keystore': {'filename': 'com.nachocove.mail.keystore',
                         'alias': 'com.nachocove.mail',
                         },
            'backup.api_key': '',
            'fileprovider': 'com.nachocove.fileprovider',
        },
        'aws': {
            'prefix': 'prod',
            'account_id': '',
            'identity_pool_id': '',
            'unauth_role_arn': '',
            'auth_role_arn': '',
            's3_bucket': '59f1d07a-prod-t3-',
            'support_s3_bucket': '59f1d07a-prod-t3-trouble-tickets',
        },
        'pinger': {
            'hostname': '',
            'root_cert': 'beta-pinger.pem',
            'crl_signing_cert': [],
        },
        'google': {
            'client_id': '',
            'client_secret': '',
        }
    },
}

def main():
    el = projects
    for arg in sys.argv[1:]:
        el = el.get(arg, None)
        if el is None:
            sys.exit(1)
    print el

if __name__ == '__main__':
    main()
