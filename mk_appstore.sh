#!/bin/sh

# USAGE: mk_appstore.sh [BRANCH] [VERSION] [BUILD]
#
# For example, mk_appstore.sh master 0.9 123 or mk_appstore.sh throttle_v1.0 1.0.appstore 321
#
# BRANCH, VERSION, and BUILD are all strings. They preferably have no space. But if they
# do contain spaces, please use ".

if [ $# -ne 3 ]; then
   echo "USAGE: mk_appstore.sh [BRANCH] [VERSION] [BUILD]"
   echo "\nFor example,"
   echo "mk_appstore.sh throttle_v1.0 1.0.beta 321 - build 1.0(321) off throttle_v1.0\n"
   exit 1
fi
branch=$1
version=$2
build=$3
tag="v$version""_$build"

die () {
  echo "ERROR: $1"
  exit 1
}

# Fetch all git repos and check out the tag
source repos.sh
./fetch.py $repos
./scripts/repos.py checkout-tag --tag "$tag" || die "fail to switch to tag $tag"

# Build everything else
timestamp=`date "+%Y%m%d_%H%M%S"`
logfile="appstore_build.$tag.$timestamp.log"
make -f build.mk 2>&1 | tee $logfile
if [ $? -ne 0 ]
then
    echo "Fail to build auxillary packages"
    exit 1
fi

# Build NachoClient
VERSION="$version" BUILD="$build" RELEASE="appstore" /Applications/Xamarin\ Studio.app/Contents/MacOS/XamarinStudio ./NachoClient.sln
if [ $? -eq 0 ]
then
    echo "appstore build $tag is made."
else
    echo "appstore build fails!"
fi
