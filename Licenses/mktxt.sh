#! /bin/bash

LIST="MimeKit \
    WbXML \
    bc-csharp \
    iCarousel \
    Antlr \
    DDay.iCal \
    DnDns \
    Facebook \
    Hockeyapp \
    JSON.Net \
    MCSwipeTableViewCell \
    Parse \
    Reachability \
    SFHFKeychainUtils \
    TTTAttributedLabel \
    UIImageEffects \
    protobuf-c \
    registered-domain-libs"

FILE=LegalInfo.txt

rm -f $FILE

for x in $LIST; do
    echo ----- >> $FILE
    cat $x/nc.txt >> $FILE
done
