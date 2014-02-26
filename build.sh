# THIS IS NOT COMPLETE
# Builds the sibling directories needed to build NachoClientX.
# You may be tempted to split this into two files -- fetch & build.

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

function doMake()
{
    pushd ../$1
    make $2 $3 $4
    if [ $? -ne 0 ]; then
        echo FAILED make $2 $3 $4
        exit $?
    fi
    popd
}

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

doMake iCarouselBinding
doMake UIImageEffects
doMake SWRevealViewControllerBinding
doMake MCSwipeTableViewCellBinding
doMake NachoPlatformBinding
doMake bc-csharp -f ../NachoClientX/bc-csharp.mk
doMake MimeKit -f ../NachoClientX/MimeKit.mk
doMake DnDns/SourceCode/DnDns -f ../../../NachoClientX/DnDns.mk
doMake DDay-iCal-Xamarin

# Build ebedded native code.
pushd native.iOS
make
popd
pushd native.Android
make
popd

# Build NachoClientX
make -f Makefile
