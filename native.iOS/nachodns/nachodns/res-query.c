//
//  nacho-res-query.c
//  NachoPlatform
//
//  Created by David Olsen on 12/17/14.
//  Copyright (c) 2014 Nacho Cove, Inc. All rights reserved.
//

#include <netinet/in.h>
#include <arpa/nameser.h>
#include <resolv.h>
#include <netdb.h>

/*
 * res_query() stores its error code in h_errno, not errno.  C# code doesn't
 * have an easy way to access h_errno.  So create a wrapper around res_query()
 * that gets the error code and returns it through a reference parameter.
 */
int nacho_res_query(const char *host, int dnsClass, int dnsType, unsigned char *answer, int answerLength, int *errorCode)
{
    int rc = res_query(host, dnsClass, dnsType, answer, answerLength);
    if (0 < rc) {
        *errorCode = 0;
    } else {
        *errorCode = h_errno;
    }
    return rc;
}
