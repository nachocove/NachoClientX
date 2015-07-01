repos = (
    'Citrix-Concierge',
    'Reachability',
    'registered-domain-libs',
    'SwipeView',
    'SwipeViewBinding',
    'UIImageEffects',
    'SWRevealViewController',
    'SWRevealViewControllerBinding',
    'ios-openssl',
    'NachoPlatformBinding',
    'NachoUIMonitorBinding',
    'bc-csharp',
    'MimeKit',
    'MailKit',
    'DnDns',
    'DDay-iCal-Xamarin',
    'CSharp-Name-Parser',
    'Telemetry',
    'aws-sdk-xamarin',
    'ModernHttpClient',
    'lucene.net-3.0.3',
    'MobileHtmlAgilityPack',
    'Google.iOS',
    # This is always the last one
    'NachoClientX'
)

branch_exceptions = {
    'bc-csharp': {
        # bc-csharp is fixed to this branch no matter what
        'fixed-branch': 'visual-studio-2010'
    }
}

# You can run this script:
#
# python repos_cfg.py
#
# in order to verify the repo list.
if __name__ == '__main__':
    n = 0
    for r in repos:
        n += 1
        print '%d: %s' % (n, r)
