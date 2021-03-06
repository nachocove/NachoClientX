#! /bin/bash

LIST="NachoClientX \
MimeKit \
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
    protobuf-c \
    registered-domain-libs \
    sqlite-net \
    MailKit \
    Microsoft \
    CSharp-Name-Parser \
    ios-openssl"

FILE=LegalInfo.txt

rm -f $FILE

for x in $LIST; do
    echo ----- >> $FILE
    cat $x/nc.txt >> $FILE
done
