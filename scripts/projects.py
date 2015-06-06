# This file lists all build-configurable parameters of Nacho Mail.
# Most of the parameters go into BuildInfo.cs
#
# The following are the dictionary keys:
# 1. Build target - dev, alpha, beta, appstore
# 2. Component - Often a particular endpoints - hockeyapp, aws, pinger. But it can be anything that groups a set of
#                parameters.

projects = {
    'dev': {
        'ios': {
            'bundle_id': 'com.nachocove.nachomail',
            'display_name': '[dev] Nacho Mail'
        },
        'hockeyapp': {
            'app_id': 'b22a505d784d64901ab1abde0728df67',
            'api_token': 'dbccf0190d5b410e8f43ef2b5e7d6b43'
        },
        'aws': {
            'prefix': 'dev',
            'account_id': '263277746520',
            'identity_pool_id': 'us-east-1:b3323849-deda-440a-a225-03043e591ec7',
            'unauth_role_arn': 'arn:aws:iam::263277746520:role/nachomail/cognito/nachomail_dev_UnAuth_DefaultRole',
            'auth_role_arn': 'NO PUBLIC AUTHENTICATION',
            's3_bucket': 'c6ae00d0-e259-4bfc-903d-5b6bc62cd651-dev-telemetry',
        },
        'pinger': {
            'hostname': 'pinger.officetaco.com',
            'root_cert': 'pinger.pem'
        }
    },
    'alpha': {
        'ios': {
            'bundle_id': 'com.nachocove.nachomail.alpha',
            'display_name': 'Nacho Mail',
            'icon_script': 'alpha/copy.sh'
        },
        'hockeyapp': {
            'app_id': 'f0c98aa84e693061fbbf3d60bb6ab1fc',
            'api_token': '4a472c5e774a4004a4eb1dd648b8af8a',
        },
        'aws': {
            'prefix': 'alpha',
            'account_id': '263277746520',
            'identity_pool_id': 'us-east-1:667b2a39-05d8-4035-a078-2f5afb82a6b8',
            'unauth_role_arn': 'arn:aws:iam::263277746520:role/nachomail/cognito/nachomail_alpha_UnAuth_DefaultRole',
            'auth_role_arn': 'NO PUBLIC AUTHENTICATION',
            's3_bucket': 'd3daf3ef-2391-41e6-ba37-3b297191751a-alpha-telemetry',
        },
        'pinger': {
            'hostname': 'alphapinger.officetaco.com',
            'root_cert': 'beta-pinger.pem'
        }
    },
    'beta': {
        'ios': {
            'bundle_id': 'com.nachocove.nachomail.beta',
            'display_name': 'Nacho Mail',
            'icon_script': 'beta/copy.sh'
        },
        'hockeyapp': {
            'app_id': '44dae4a6ae9134930c64c623d5023ac4',
            'api_token': '1c08642c07d244f7a0600ef5654e0dad'
        },
        'aws': {
            'prefix': 'beta',
            'account_id': '610813048224',
            'identity_pool_id': 'us-east-1:0d40f2cf-bf6c-4875-a917-38f8867b59ef',
            'unauth_role_arn': 'arn:aws:iam::610813048224:role/Cognito_dev_telemetryUnauth_DefaultRole',
            'auth_role_arn': 'NO PUBLIC AUTHENTICATION',
            's3_bucket': '',
        },
        'pinger': {
            'hostname': 'dk65t.pxs001.com',
            'root_cert': 'beta-pinger.pem'
        }
    },
    'appstore': {
        'ios': {
            'bundle_id': 'com.nachocove.mail',
            'display_name': 'Nacho Mail',
            'icon_script': 'appstore/copy.sh'
        },
        'hockeyapp': {
            'app_id': 'df752a5c4c7bb503fac6e26b0f0dcafa',
            'api_token': '0344908b24aa498288268a726d028332'
        },
        'aws': {
            'prefix': 'prod',
            'account_id': '610813048224',
            'identity_pool_id': 'us-east-1:2e5f8d44-3f75-4f94-9539-696849b9bca5',
            'unauth_role_arn': 'arn:aws:iam::610813048224:role/nachomail/cognito/nachomail_prod_UnAuth_DefaultRole',
            'auth_role_arn': 'NO PUBLIC AUTHENTICATION',
            's3_bucket': '',
        },
        'pinger': {
            'hostname': 'p745x.pxs001.com',
            'root_cert': 'beta-pinger.pem'
        }
    },

}
