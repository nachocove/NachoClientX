# Fetch all sources from git

tag=$1
message=$2

function gitUntag()
{
    pushd "../$1" 1> /dev/null
	echo "Deleting $tag in `pwd`..."
	git tag -d $tag
	popd 1> /dev/null
}

gitUntag Parse
gitUntag Crashlytics
gitUntag Reachability
gitUntag registered-domain-libs
gitUntag iCarousel
gitUntag iCarouselBinding
gitUntag UIImageEffects
gitUntag SWRevealViewController
gitUntag SWRevealViewControllerBinding
gitUntag MCSwipeTableViewCell
gitUntag MCSwipeTableViewCellBinding
gitUntag NachoPlatformBinding
gitUntag bc-csharp
gitUntag MimeKit
gitUntag DnDns
gitUntag DDay-iCal-Xamarin
gitUntag NachoClientX