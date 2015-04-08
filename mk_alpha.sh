#!/bin/sh

# USAGE: mk_alpha.sh [VERSION] [BUILD]
#
# For example, mk_alpha.sh 0.8 123 or mk_alpha.sh 1.0.alpha 321
#
# VERSION and BUILD are both strings. They preferably have no space. But if they
# do contain spaces, please use ".

if [ $# -ne 2 ]; then
   echo "USAGE: mk_alpha.sh [VERSION] [BUILD]"
   exit 1
fi

# Fetch all git repos
source repos.sh
./fetch.py $repos

# Tag all repos
tag="v$1_$2"
sh tag.sh "$tag" "$tag"

# Build everything else
timestamp=`date "+%Y%m%d_%H%M%S"`
logfile="alpha_build.$tag.$timestamp.log"
make -f build.mk 2>&1 | tee $logfile
if [ $? -neq 0 ]
then
    echo "Fail to build auxillary packages"
    exit 1
fi

# Build NachoClient
VERSION="$1" BUILD="$2" RELEASE="alpha" make release 2>&1 | tee -a $logfile
if [ $? -eq 0 ]
then
    sh push_tag.sh "$tag"
    echo "Build $tag is made."
else
    echo "Build fails!"
fi

