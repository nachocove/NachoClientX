#!/bin/sh

# USAGE: mk_appstore.sh [VERSION] [BUILD]
#
# For example, mk_appstore.sh 0.9 123 or mk_appstore.sh 1.0.appstore 321
#
# VERSION and BUILD are both strings. They preferably have no space. But if they
# do contain spaces, please use ".

if [ $# -ne 2 ]; then
   echo "USAGE: mk_appstore.sh [VERSION] [BUILD]"
   exit 1
fi

# Fetch all git repos
source repos.sh
./fetch.py $repos

# Tag all repos
tag="v$1_$2"
sh checkout_tag.sh "$tag"

# Build everything else
timestamp=`date "+%Y%m%d_%H%M%S"`
logfile="appstore_build.$tag.$timestamp.log"
make -f build.mk 2>&1 | tee $logfile
if [ $? -neq 0 ]
then
    echo "Fail to build auxillary packages"
    exit 1
fi

# Build NachoClient
VERSION="$1" BUILD="$2" RELEASE="appstore" make release 2>&1 | tee -a $logfile
if [ $? -eq 0 ]
then
    echo "appstore build $tag is made."
else
    echo "appstore build fails!"
fi
