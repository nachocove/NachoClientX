# This is the list of git repos shared by fetch.sh, tag.sh, untag.sh,
# and push_tag.sh. NachoClientX is not included because fetch.sh
# does not pull NachoClient.git.

# NOTE: ios-openssl must be in front NachoPlatformBinding as it needs to installs
#       its C headers after build before NachoPlatformBinding can compile.

repos="
Reachability
registered-domain-libs
SwipeView
SwipeViewBinding
UIImageEffects
SWRevealViewController
SWRevealViewControllerBinding
ios-openssl
NachoPlatformBinding
NachoUIMonitorBinding
bc-csharp
MimeKit
MailKit
DnDns
DDay-iCal-Xamarin
Telemetry
aws-sdk-xamarin
ModernHttpClient
lucene.net-3.0.3
MobileHtmlAgilityPack
CSharp-Name-Parser
Google.iOS
"
