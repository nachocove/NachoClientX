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
   echo "mk_alpha.sh throttle_v1.0 1.0.alpha 321 - build 1.0.alpha(321) off throttle_v1.0"
   echo "mk_alpha.sh test 1.0.alpha 321 - build 1.0.alpha(321) to test\n"
   exit 1
fi
branch=$1
version=$2
build=$3
tag="v$version""_$build"

if [ $branch == "test" ] ; then
   RUN=echo
else
   RUN=
fi

die () {
  echo "ERROR: $1"
  exit 1
}

# Fetch all git repos and switch to the specific branch
$RUN ./scripts/fetch.py || die "fail to fetch all repos!"
$RUN ./scripts/repos.py checkout-branch --branch $branch || die "fail to switch to branch $branch"

# Need to fetch and change branch again because the branch may add new repos that is not
# in master's repos_cfg.py.
$RUN ./scripts/fetch.py || die "fail to fetch all repos!"
$RUN ./scripts/repos.py checkout-branch --branch $branch || die "fail to switch to branch $branch"

# Build everything else
timestamp=`date "+%Y%m%d_%H%M%S"`
logfile="alpha_build.$tag.$timestamp.log"
make -f build.mk 2>&1 | tee $logfile
if [ ${PIPESTATUS[0]} -ne 0 ]
then
    echo "Fail to build auxillary packages"
    exit 1
fi

# Build NachoClient
VERSION="$version" BUILD="$build" RELEASE="alpha" make release 2>&1 | tee -a $logfile
if [ ${PIPESTATUS[0]} -eq 0 ]
then
    # Tag & push tags for all repos
    $RUN ./scripts/repos.py create-tag --version "$version" --build "$build" || die "fail to tag all repos!"
    echo "Build $tag is made."
    (cd NachoClient.iOS; VERSION="$version" BUILD="$build" RELEASE="alpha" $RUN ../scripts/hockeyapp_upload.py --ios ./bin/iPhone/Release)
    (cd NachoClient.Android; VERSION="$version" BUILD="$build" RELEASE="alpha" $RUN ../scripts/hockeyapp_upload.py --android ./bin/Release)
else
    echo "Build fails!"
fi
