# Fetch all sources from git

function gitUpdate()
{
    pushd ..
    if [ ! -d "$1" ]; then
        git clone git@github.com:nachocove/"$1".git
        if [  $? -ne 0 ]; then
            echo FAILED git clone git@github.com:nachocove/"$1".git
            exit $?
        fi
    fi
    pushd "$1"
    git pull 
    if [ $? -ne 0 ]; then
        echo FAILED git pull "$1"
        exit $?
    fi
    popd
    popd
}

gitUpdate Crashlytics
gitUpdate Reachability
gitUpdate registered-domain-libs
gitUpdate iCarousel
gitUpdate iCarouselBinding
gitUpdate UIImageEffects
gitUpdate SWRevealViewController
gitUpdate SWRevealViewControllerBinding
gitUpdate MCSwipeTableViewCell
gitUpdate MCSwipeTableViewCellBinding
gitUpdate NachoPlatformBinding
gitUpdate bc-csharp
pushd ../bc-csharp; git checkout -b visual-studio-2010 origin/visual-studio-2010; popd
gitUpdate MimeKit
gitUpdate DnDns
gitUpdate DDay-iCal-Xamarin
