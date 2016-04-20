#!/bin/sh

for i in `grep 'Starting DnldEmailBodyCmd' $1 | cut -d'/' -f2 | cut -d' ' -f 1`; do grep $i $1 ; echo; done
