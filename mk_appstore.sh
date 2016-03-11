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
release=appstore

die () {
  echo "ERROR: $1"
  exit 1
}

# Fetch all git repos and check out the tag
./scripts/fetch.py
./scripts/repos.py checkout-tag --tag "$tag" || die "failed to switch to tag $tag"

# Need to fetch and change branch again because the branch may add new repos that is not
# in master's repos_cfg.py.
./scripts/fetch.py
./scripts/repos.py checkout-tag --tag "$tag" || die "failed to switch to tag $tag"

# Build everything else
timestamp=`date "+%Y%m%d_%H%M%S"`
logfile=$release"_build.$tag.$timestamp.log"
make -f build.mk 2>&1 | tee $logfile
if [ ${PIPESTATUS[0]} -ne 0 ]
then
    echo "Fail to build auxillary packages"
    exit 1
fi

ANDROID_PACKAGE=`./scripts/projects.py $release android package_name`
if [ -z "$ANDROID_PACKAGE" ] ; then
    echo "No package name found in projects"
    exit 1
fi
ORIGINAL_APK="$ANDROID_PACKAGE.apk"
EXPECTED_APK="$ANDROID_PACKAGE.apk"
RESIGNED_APK="$ANDROID_PACKAGE-tmp.apk"


# Build NachoClient.iOS
VERSION="$version" BUILD="$build" RELEASE="$release" /Applications/Xamarin\ Studio.app/Contents/MacOS/XamarinStudio ./NachoClient.sln
if [ ${PIPESTATUS[0]} -eq 0 ]
then
    (cd NachoClient.iOS; VERSION="$version" BUILD="$build" RELEASE="$release" ../scripts/hockeyapp_upload.py --no-skip --ios ./bin/iPhone/AppStore) || die "Failed to upload ipa"
else
    echo "appstore build failed!"
fi

# Build NachoClient.Android
(cd NachoClient.Android; 
    rm -f ` find . -name "*.apk" `
    VERSION="$version" BUILD="$build" RELEASE="$release" scripts/mk_log_settings.py
    VERSION="$version" BUILD="$build" RELEASE="$release" ../scripts/configure_android.py ./Properties/AndroidManifest.xml
    VERSION="$version" BUILD="$build" RELEASE="$release" ../scripts/mk_build_info.py --architecture android --root . --csproj-file NachoClient.Android.csproj
    xbuild "/t:SignAndroidPackage" "/p:Configuration=Release" NachoClient.Android.csproj
    ../scripts/android_sign.py sign --release $release --keystore-path=$HOME/.ssh ./bin/Release/$ORIGINAL_APK ./bin/Release/$RESIGNED_APK || die "Failed to re-sign apk";
    mv ./bin/Release/$RESIGNED_APK ./bin/Release/$EXPECTED_APK || die "Failed to move apk";
    VERSION="$version" BUILD="$build" RELEASE="$release" ../scripts/hockeyapp_upload.py --no-skip --android ./bin/Release || die "Failed to upload apk";
) || die "Could not sign and upload apk"
