#!/bin/sh

# USAGE: mk_beta.sh [BRANCH] [VERSION] [BUILD]
#
# For example, mk_beta.sh 0.9 123 or mk_beta.sh 1.0.beta 321
#
# BRANCH, VERSION, and BUILD are all strings. They preferably have no space. But if they
# do contain spaces, please use ".

if [ $# -ne 3 ]; then
   echo "USAGE: mk_beta.sh [BRANCH] [VERSION] [BUILD]"
   echo "\nFor example,"
   echo "mk_beta.sh master 0.9 123 - build 0.9(123) off master"
   echo "mk_beta.sh throttle_v1.0 1.0.beta 321 - build 1.0.beta(321) off throttle_v1.0\n"
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

# Fetch all git repos and check out the tag.
./scripts/fetch.py || die "fail to fetch all repos!"
./scripts/repos.py checkout-tag --tag "$tag" || die "fail to switch to tag $tag"

# Check if the branch matches the given one

# Build everything else
timestamp=`date "+%Y%m%d_%H%M%S"`
logfile="beta_build.$tag.$timestamp.log"
make -f build.mk 2>&1 | tee $logfile
if [ ${PIPESTATUS[0]} -ne 0 ]
then
    echo "Fail to build auxillary packages"
    exit 1
fi

# Build NachoClient
VERSION="$version" BUILD="$build" RELEASE="beta" make release 2>&1 | tee -a $logfile
if [ ${PIPESTATUS[0]} -eq 0 ]
then
    echo "Beta build $tag is made."
    (cd NachoClient.iOS; VERSION="$version" BUILD="$build" RELEASE="beta" ../scripts/hockeyapp_upload.py --no-skip --ios ./bin/iPhone/Release)
    (cd NachoClient.Android; VERSION="$version" BUILD="$build" RELEASE="beta" ../scripts/hockeyapp_upload.py --no-skip --android ./bin/Release)
else
    echo "Beta build fails!"
fi
