# Fetch all sources from git

tag=$1
message=$2

function gitTag()
{
    pushd "../$1" 1> /dev/null
	echo "Tagging `pwd` as $tag..."
	git tag -a $tag -m $message
	popd 1> /dev/null
}

gitTag Parse
gitTag Crashlytics
gitTag Reachability
gitTag registered-domain-libs
gitTag iCarousel
gitTag iCarouselBinding
gitTag UIImageEffects
gitTag SWRevealViewController
gitTag SWRevealViewControllerBinding
gitTag MCSwipeTableViewCell
gitTag MCSwipeTableViewCellBinding
gitTag NachoPlatformBinding
gitTag bc-csharp
gitTag MimeKit
gitTag DnDns
gitTag DDay-iCal-Xamarin
gitTag NachoClientX