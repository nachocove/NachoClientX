repos = (
    'Reachability',
    'registered-domain-libs',
    'ios-openssl',
    'NachoPlatformBinding',
    'NachoUIMonitorBinding',
    'MailKit',
    'DnDns',
    'DDay-iCal-Xamarin',
    'CSharp-Name-Parser',
    'aws-sdk-xamarin',
    'lucene.net-3.0.3',
    'MobileHtmlAgilityPack',
    'JetBlack.Caching',
    'TokenAutoComplete',
    'TokenAutoCompleteBinding',
    'rtfparserkit',
    'rtfparserkitBinding',
    'OkHttp-Xamarin',
    # # This is always the last one
    'NachoClientX'
)

branch_exceptions = {
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
