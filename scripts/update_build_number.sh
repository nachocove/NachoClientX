#!/bin/sh
if [ -f .build_number.new ]; then
  echo "Updating build number..."
  mv .build_number.new .build_number
fi

