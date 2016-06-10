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
            'display_name': '[dev] Nacho Mail',
            'file_sharing': True,
            'app_group': 'group.com.nachocove.nachomail',
            'hockeyapp': {'app_id': 'b22a505d784d64901ab1abde0728df67', 'api_token': 'dbccf0190d5b410e8f43ef2b5e7d6b43'},
            'icloud_container': 'iCloud.com.nachocove.nachomail'
        },
        'ios_share': {
            'bundle_id': 'com.nachocove.nachomail.share',
            'app_group': 'group.com.nachocove.nachomail',
            'display_name': '[dev] Nacho Mail'
        },
        'android': {
            'package_name': 'com.nachocove.nachomail.dev',
            'label': '[dev] Nacho Mail',
            'hockeyapp': {'app_id': '6308748f44bb7da155f7c44c076e8201', 'api_token': 'd7565337373147299f4b75adfacd6efa'},
            'keystore': {'filename': '',
                         'alias': '',
                         },
            'backup.api_key': 'AEdPqrEAAAAIHhu7nvviRVUbM-wMP0XfbO1OAPy579Irm97hJw',
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
            'client_id': '135541750674-l44k46h09u2obl3upnchl9lt3nicfd52.apps.googleusercontent.com',
            'client_secret': '3NKmUHorCm8_IS4lkOWuN_7i',
        }
    },
    'alpha': {
        'ios': {
            'bundle_id': 'com.nachocove.nachomail.alpha',
            'display_name': 'Nacho Mail',
            'icon_script': 'alpha/copy.sh',
            'file_sharing': True,
            'app_group': 'group.com.nachocove.nachomail.alpha',
            'hockeyapp': {'app_id': 'f0c98aa84e693061fbbf3d60bb6ab1fc', 'api_token': '4a472c5e774a4004a4eb1dd648b8af8a'},
            'icloud_container': 'iCloud.com.nachocove.nachomail.alpha'
        },
        'ios_share': {
            'bundle_id': 'com.nachocove.nachomail.alpha.share',
            'app_group': 'group.com.nachocove.nachomail.alpha',
            'display_name': 'Nacho Mail'
        },
        'android': {
            'package_name': 'com.nachocove.nachomail.alpha',
            'label': 'Nacho Mail',
            'icon_script': 'alpha/copy.sh',
            'hockeyapp': {'app_id': '3f057536fb00405eb9e3542231831964', 'api_token': '4c8a0529cb7241cf8bcc49b2e8387db8'},
            'keystore': {'filename': 'com.nachocove.mail.alpha.keystore',
                         'alias': 'com.nachocove.mail.alpha',
                         },
            'backup.api_key': 'AEdPqrEAAAAI6nEa_tRWJ5NsItWcRQ4vDQVTyXlzBZA34k1MJQ',
            'fileprovider': 'com.nachocove.alpha.fileprovider',
        },
        'aws': {
            'prefix': 'alpha',
            'account_id': '263277746520',
            'identity_pool_id': 'us-east-1:667b2a39-05d8-4035-a078-2f5afb82a6b8',
            'unauth_role_arn': 'arn:aws:iam::263277746520:role/nachomail/cognito/nachomail_alpha_UnAuth_DefaultRole',
            'auth_role_arn': 'NO PUBLIC AUTHENTICATION',
            's3_bucket': 'd3daf3ef-alpha-t3-',
            'support_s3_bucket': 'd3daf3ef-alpha-t3-trouble-tickets',
        },
        'pinger': {
            'hostname': 'alphapinger.officetaco.com',
            'root_cert': 'beta-pinger.pem',
            'crl_signing_cert': [],
        },
        'google': {
            'client_id': '135541750674-ggdpk07n9rd91j9479u685ud6usqodrq.apps.googleusercontent.com',
            'client_secret': 'uCnNdx_SuZnsa9UZkhlUcbf6',
        }
    },
    'beta': {
        'ios': {
            'bundle_id': 'com.nachocove.nachomail.beta',
            'display_name': 'Nacho Mail',
            'icon_script': 'beta/copy.sh',
            'file_sharing': False,
            'app_group': 'group.com.nachocove.nachomail.beta',
            'hockeyapp': {'app_id': '44dae4a6ae9134930c64c623d5023ac4', 'api_token': '1c08642c07d244f7a0600ef5654e0dad'},
            'icloud_container': 'iCloud.com.nachocove.nachomail.beta'
        },
        'ios_share': {
            'bundle_id': 'com.nachocove.nachomail.beta.share',
            'app_group': 'group.com.nachocove.nachomail.beta',
            'display_name': 'Nacho Mail'
        },
        'android': {
            'package_name': 'com.nachocove.nachomail',
            'label': 'Nacho Mail',
            'icon_script': 'beta/copy.sh',
            'hockeyapp': {'app_id': 'bf2582dd142f473dbfdc3bdb8349a3b5', 'api_token': '059371f4a7db486fbbb1bebcb3965aaa'},
            'keystore': {'filename': 'com.nachocove.mail.keystore',
                         'alias': 'com.nachocove.mail',
                         },
            'backup.api_key': 'AEdPqrEAAAAIWORF8SdqdCn_lVgJTP6lxCod1PGW3He-2OIV0g',
            'fileprovider': 'com.nachocove.fileprovider',
        },
        'aws': {
            'prefix': 'beta',
            'account_id': '610813048224',
            'identity_pool_id': 'us-east-1:0d40f2cf-bf6c-4875-a917-38f8867b59ef',
            'unauth_role_arn': 'arn:aws:iam::610813048224:role/nachomail/cognito/nachomail_beta_UnAuth_DefaultRole',
            'auth_role_arn': 'NO PUBLIC AUTHENTICATION',
            's3_bucket': '3ca28b5e-beta-t3-',
            'support_s3_bucket': '3ca28b5e-beta-t3-trouble-tickets',
        },
        'pinger': {
            'hostname': 'dk65t.pxs001.com',
            'root_cert': 'beta-pinger.pem',
            'crl_signing_cert': [],
        },
        'google': {
            'client_id': '135541750674-d6o0v1h299isdh155thlo01r7d1sj22v.apps.googleusercontent.com',
            'client_secret': 'w_STpstIgIVrPYcCEYclC3KT',
        }
    },
    'appstore': {
        'ios': {
            'bundle_id': 'com.nachocove.mail',
            'display_name': 'Nacho Mail',
            'icon_script': 'appstore/copy.sh',
            'file_sharing': False,
            'app_group': 'group.com.nachocove.mail',
            'hockeyapp': {'app_id': 'df752a5c4c7bb503fac6e26b0f0dcafa', 'api_token': '0344908b24aa498288268a726d028332'},
            'icloud_container': 'iCloud.com.nachocove.mail'
        },
        'ios_share': {
            'bundle_id': 'com.nachocove.mail.share',
            'app_group': 'group.com.nachocove.mail',
            'display_name': 'Nacho Mail'
        },
        'android': {
            'package_name': 'com.nachocove.nachomail',
            'label': 'Nacho Mail',
            'icon_script': 'appstore/copy.sh',
            'hockeyapp': {'app_id': 'a62575b6e71e118ecc44e775d6f5db88', 'api_token': 'c1c7e717a6da4ba7b4a3408c9ec60418'},
            'keystore': {'filename': 'com.nachocove.mail.keystore',
                         'alias': 'com.nachocove.mail',
                         },
            'backup.api_key': 'AEdPqrEAAAAIEP3e9PW3CpJ8D8MpJDOAg2dugLzFunXiC9LbfA',
            'fileprovider': 'com.nachocove.fileprovider',
        },
        'aws': {
            'prefix': 'prod',
            'account_id': '610813048224',
            'identity_pool_id': 'us-east-1:2e5f8d44-3f75-4f94-9539-696849b9bca5',
            'unauth_role_arn': 'arn:aws:iam::610813048224:role/nachomail/cognito/nachomail_prod_UnAuth_DefaultRole',
            'auth_role_arn': 'NO PUBLIC AUTHENTICATION',
            's3_bucket': '59f1d07a-prod-t3-',
            'support_s3_bucket': '59f1d07a-prod-t3-trouble-tickets',
        },
        'pinger': {
            'hostname': 'p745x.pxs001.com',
            'root_cert': 'beta-pinger.pem',
            'crl_signing_cert': [],
        },
        'google': {
            'client_id': '135541750674-f307j8582mi397cd0hcsbtn8ts1djmdv.apps.googleusercontent.com',
            'client_secret': '5iBJeVUWnYs4jkHbDZziV0Gl',
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
