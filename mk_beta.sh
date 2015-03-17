#!/bin/sh

# USAGE: mk_beta.sh [VERSION] [BUILD]
#
# For example, mk_beta.sh 0.9 123 or mk_beta.sh 1.0.beta 321
#
# VERSION and BUILD are both strings. They preferably have no space. But if they
# do contain spaces, please use ".

if [ $# -ne 2 ]; then
   echo "USAGE: mk_beta.sh [VERSION] [BUILD]"
   exit 1
fi

# Fetch all git repos
source repos.sh
./fetch.py $repos

# Tag all repos
tag="v$1_$2"
sh checkout_tag.sh "$tag"

# Build everything else
make -f build.mk
if [ $? -neq 0 ]
then
    echo "Fail to build auxillary packages"
    exit 1
fi

# Build NachoClient
VERSION="$1" BUILD="$2" RELEASE="beta" make release
if [ $? -eq 0 ]
then
    echo "Beta build $tag is made."
else
    echo "Beta build fails!"
fi
