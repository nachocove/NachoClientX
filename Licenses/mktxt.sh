#! /bin/bash

LIST="MimeKit \
    WbXML \
    bc-csharp \
    Antlr \
    DDay.iCal \
    DnDns \
    Hockeyapp \
    JSON.Net \
    ModernHttpClient \
    MobileHtmlAgilityPack \
    lucene.net \
    okhttp \
    Reachability \
    SFHFKeychainUtils \
    SwipeView \
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
