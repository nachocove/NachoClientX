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
    make
    if [ $? -ne 0 ]; then
        echo FAILED make "$1"
        exit $?
    fi
    popd
}

gitUpdate iCarousel
gitUpdate iCarouselBinding
gitUpdate UIImageEffects

doMake iCarouselBinding
doMake UIImageEffects

