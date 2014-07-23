#!/bin/sh

# Fetch all git repos
sh fetch.sh

# Tag all repos
sh tag.sh "v$1_$2" "v$1_$2"

# Build everything else
make -f build.mk

# Build NachoClient
VERSION="$1" BUILD="$2" make beta
