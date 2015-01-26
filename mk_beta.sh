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
sh fetch.sh

# Tag all repos
# sh checkout_tag.sh "v$1_$2"

# Build everything else
make -f build.mk

# Build NachoClient
VERSION="$1" BUILD="$2" RELEASE="beta" make release
