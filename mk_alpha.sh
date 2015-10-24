#!/bin/sh

source ./scripts/build_lib.sh

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
make_target=release
timestamp=`date "+%Y%m%d_%H%M%S"`
logfile=$release"_build.$tag.$timestamp.log"

# Fetch all git repos and switch to the specific branch
fetch_branch $branch

# Need to fetch and change branch again because the branch may add new repos that is not
# in master's repos_cfg.py.
fetch_branch $branch

# Build everything else
build_everything $logfile

# Build NachoClient
build_nachoclient $make_target $version $build $release $logfile || die "Build Failed!"

# Tag & push tags for all repos
create_tag $tag $version $build

(cd NachoClient.iOS; upload_ios ./bin/iPhone/Ad-Hoc $version $build $release)
(cd NachoClient.Android; sign_and_upload_android ./bin/Release $version $build $release)
