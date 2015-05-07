repos = (
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
    'DnDns',
    'DDay-iCal-Xamarin',
    'Telemetry',
    'aws-sdk-xamarin',
    'ModernHttpClient',
    'lucene.net-3.0.3',
    'MobileHtmlAgilityPack',
    # This is always the last one
    'NachoClientX'
)

branch_exceptions = {
    'bc-csharp': {
        # bc-csharp is fixed to this branch no matter what
        'fixed-branch': 'visual-studio-2010'
    }
}