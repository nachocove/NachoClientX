#!/bin/sh

source ./scripts/build_lib.sh

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
make_target=release
timestamp=`date "+%Y%m%d_%H%M%S"`
logfile=$release"_build.$tag.$timestamp.log"
no_skip=1

# Fetch all git repos and check out the tag.
fetch_tag $tag

# Build everything else
build_everything $logfile

# Build NachoClient
build_nachoclient $make_target $version $build $release $logfile || die "Build Failed!"

(cd NachoClient.iOS; upload_ios ./bin/iPhone/AppStore $version $build $release $no_skip)
(cd NachoClient.Android; sign_and_upload_android ./bin/Release $version $build $release $no_skip)
