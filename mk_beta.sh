#!/bin/sh
sh fetch.sh
make -f build.mk
VERSION="$1" BUILD="$2" make beta
