#!/bin/sh

# USAGE: mk_alpha.sh [BRANCH] [VERSION] [BUILD]
#
# For example, mk_alpha.sh master 0.8 123 or mk_alpha.sh throttle_v1.0 1.0.alpha 321
#
# BRANCH, VERSION, and BUILD are all strings. They preferably have no space. But if they
# do contain spaces, please use ".

if [ $# -ne 3 ]; then
   echo "USAGE: mk_alpha.sh [BRANCH] [VERSION] [BUILD]"
   echo "\nFor example,"
   echo "mk_alpha.sh master 0.9 123 - build 0.9(123) off master"
   echo "mk_alpha.sh throttle_v1.0 1.0.alpha 321 - build 1.0.alpha(321) off throttle_v1.0\n"
   exit 1
fi
branch=$1
version=$2
build=$3
tag="v$version""_$build"
release=alpha

die () {
  echo "ERROR: $1"
  exit 1
}

# Fetch all git repos and switch to the specific branch
./scripts/fetch.py || die "failed to fetch all repos!"
./scripts/repos.py checkout-branch --branch $branch || die "failed to switch to branch $branch"

# Need to fetch and change branch again because the branch may add new repos that is not
# in master's repos_cfg.py.
./scripts/fetch.py || die "failed to fetch all repos!"
./scripts/repos.py checkout-branch --branch $branch || die "failed to switch to branch $branch"

# Build everything else
timestamp=`date "+%Y%m%d_%H%M%S"`
logfile=$release"_build.$tag.$timestamp.log"
make -f build.mk 2>&1 | tee $logfile
if [ ${PIPESTATUS[0]} -ne 0 ]
then
    echo "Failed to build auxillary packages"
    exit 1
fi

ANDROID_PACKAGE=`./scripts/projects.py $release android package_name`
if [ -z "$ANDROID_PACKAGE" ] ; then
    echo "No package name found in projects"
    exit 1
fi
EXPECTED_APK="$ANDROID_PACKAGE.apk"
RESIGNED_APK="$ANDROID_PACKAGE-tmp.apk"

# Build NachoClient
VERSION="$version" BUILD="$build" RELEASE="$release" make release 2>&1 | tee -a $logfile
if [ ${PIPESTATUS[0]} -eq 0 ]
then
    # Tag & push tags for all repos
    ./scripts/repos.py create-tag --version "$version" --build "$build" || die "failed to tag all repos!"
    echo "Build $tag is made."
    (cd NachoClient.iOS; VERSION="$version" BUILD="$build" RELEASE="$release" ../scripts/hockeyapp_upload.py --ios ./bin/iPhone/Ad-Hoc) || die "Failed to upload ipa"

    (cd NachoClient.Android; 
         ../scripts/android_sign.py sign --release $release --keystore-path=$HOME/.ssh ./bin/Release/$EXPECTED_APK ./bin/Release/$RESIGNED_APK || die "Failed to re-sign apk";
         mv ./bin/Release/$RESIGNED_APK ./bin/Release/$EXPECTED_APK || die "Failed to move apk";
         VERSION="$version" BUILD="$build" RELEASE="$release" ../scripts/hockeyapp_upload.py --android ./bin/Release || die "Failed to upload apk";
    ) || die "Could not sign and upload apk"
else
    echo "Build failed!"
fi
