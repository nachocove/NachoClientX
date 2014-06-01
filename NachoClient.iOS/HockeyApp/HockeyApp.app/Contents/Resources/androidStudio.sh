#!/bin/bash

if [ "$1" = "-l" ] || [ "$1" = "--line" ] ; then
    line=$2
    file=$3
else
    line=1
    file=$1
fi

/Applications/Android\ Studio.app/Contents/MacOS/studio --line "$line" "$file" &
