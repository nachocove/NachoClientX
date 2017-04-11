#!/bin/sh
./scripts/fetch.py || (echo "Could not complete fetch.py"; exit 1) || exit 1
make -f build.mk || (echo "Build failed."; exit 1) || exit 1
