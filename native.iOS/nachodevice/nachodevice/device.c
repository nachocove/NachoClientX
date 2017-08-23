#include <stdlib.h>
#include <sys/types.h>
#include <sys/sysctl.h>
#import <sys/socket.h>
#import <sys/sysctl.h>
#import <net/if.h>
#import <net/if_dl.h>
#include <stdio.h>
#include <CFNetwork/CFNetwork.h>
#include <signal.h>
#include <stdio.h>
#include <unistd.h>
#include <stdlib.h>
#include <string.h>
#include <sys/stat.h>
#include <fts.h>

#include "TargetConditionals.h"

void
nacho_sysctlbyname(char *dest, size_t limit, char *name) {
    size_t size;
    sysctlbyname(name, NULL, &size, NULL, 0);
    char *result = malloc(size);
    if (NULL == result) {
        *dest = '\0';
        return;
    }
    sysctlbyname(name, result, &size, NULL, 0);
    strncpy (dest, result, limit);
}

unsigned
nacho_is_simulator() {
    return TARGET_IPHONE_SIMULATOR;
}

void
nacho_macaddr (char *dest, size_t limit)
{
#define MIBSLOTS 6
    int                 mib[MIBSLOTS];
    char                *msgBuffer = NULL;
    size_t              length;
    unsigned char       mac[6];
    struct if_msghdr    *ifm;
    struct sockaddr_dl  *sock;
    int success = 0;
    
    mib[0] = CTL_NET;
    mib[1] = AF_ROUTE;
    mib[2] = 0;
    mib[3] = AF_LINK;
    mib[4] = NET_RT_IFLIST;
    
    if ((mib[5] = if_nametoindex("en0")) != 0)
    {
        if (sysctl(mib, MIBSLOTS, NULL, &length, NULL, 0) >= 0)
        {
            if ((msgBuffer = malloc(length)) != NULL)
            {
                if (sysctl(mib, MIBSLOTS, msgBuffer, &length, NULL, 0) >= 0) {
                    success = 1;
                }
            }
        }
    }
    if (! success) {
        strncpy(dest, "000000000000", limit);
        return;
    }
    ifm = (struct if_msghdr *) msgBuffer;
    // Map to link-level socket structure
    sock = (struct sockaddr_dl *) (ifm + 1);
    // Copy link layer address data in socket structure to an array
    memcpy(&mac, sock->sdl_data + sock->sdl_nlen, 6);
    // Read from char array into a string object, i nto traditional Mac address format
    snprintf(dest, limit, "%02x%02x%02x%02x%02x%02x",mac[0], mac[1], mac[2],
                mac[3], mac[4], mac[5]);
    free(msgBuffer);
}


#define STRSIZE 2048

// full-url.
// user-agent.
// proto-version.
// username
// password

static void
strip_slash_rn (char *target)
{
    char *slash_rn = NULL;
    if (NULL != (slash_rn = strchr (target, '\r'))) {
        *slash_rn = '\0';
    }
    if (NULL != (slash_rn = strchr (target, '\n'))) {
        *slash_rn = '\0';
    }
}


static sig_t old_bus, old_segv, old_x91;
static char where_to[STRSIZE];

static void
clear_handlers ()
{
    signal(SIGBUS, old_bus);
    signal(SIGSEGV, old_segv);
    signal(0x91, old_x91);
}

static CFHTTPMessageRef
make_request (CFURLRef url, CFStringRef user_agent, CFStringRef proto_version)
{
    /* <Provision xmlns="Provision"><RemoteWipe>1</RemoteWipe></Provision> in WBXML. */
    static UInt8 body[] = { 3, 1, 106, 0, 0, 14, 69, 76, 75, 3, 49, 0, 1, 1, 1 };
    CFHTTPMessageRef request = CFHTTPMessageCreateRequest (kCFAllocatorDefault, CFSTR ("POST"), url,kCFHTTPVersion1_1);
    CFDataRef body_data_ref = CFDataCreate (kCFAllocatorDefault, body, sizeof (body));
    CFHTTPMessageSetBody (request, body_data_ref);
    CFHTTPMessageSetHeaderFieldValue (request, CFSTR ("Content-Length"), CFSTR ("15"));
    CFHTTPMessageSetHeaderFieldValue (request, CFSTR ("Content-Type"), CFSTR ("application/vnd.ms-sync.wbxml"));
    CFHTTPMessageSetHeaderFieldValue (request, CFSTR ("User-Agent"), user_agent);
    CFHTTPMessageSetHeaderFieldValue (request, CFSTR ("X-MS-PolicyKey"), CFSTR ("0"));
    CFHTTPMessageSetHeaderFieldValue (request, CFSTR ("MS-ASProtocolVersion"), proto_version);
    return request;
}

static CFHTTPMessageRef
try_request (CFHTTPMessageRef request)
{
  CFReadStreamRef requestStream = CFReadStreamCreateForHTTPRequest (NULL, request);
    CFReadStreamSetProperty (requestStream, kCFStreamPropertyHTTPAttemptPersistentConnection, kCFBooleanTrue);
  CFReadStreamOpen(requestStream);

  CFIndex numBytesRead = 0 ;
   do {
     static UInt8 buf[1024];
     numBytesRead = CFReadStreamRead(requestStream, buf, sizeof(buf));
   } while(numBytesRead > 0);

   CFHTTPMessageRef response = (CFHTTPMessageRef)CFReadStreamCopyProperty(requestStream, kCFStreamPropertyHTTPResponseHeader);

   CFReadStreamClose (requestStream);
   return response;
}

int
wipe_underneath (char *top)
{
    int retval = 1;
    char *path_argv[] = { top, NULL };
    FTS *tree = NULL;
    FTSENT *node = NULL;
    tree = fts_open (path_argv, FTS_PHYSICAL, NULL);
    while (NULL != (node = fts_read (tree))) {
        if (! strcmp (node->fts_accpath, top)) {
            /* don't act on the top directory. */
            continue;
        }
        switch (node->fts_info) {
            case FTS_DP:
            case FTS_DC:
                /* remove the dir on the 2nd visit, after suborinates are deleted. */
                if (0 > rmdir (node->fts_accpath)) {
                    retval = 0;
                    perror (NULL);
                }
                break;
            case FTS_DEFAULT:
            case FTS_F:
            case FTS_SL:
            case FTS_SLNONE:
                if (0 > unlink (node->fts_accpath)) {
                    retval = 0;
                    perror (NULL);
                }
                break;
            case FTS_DNR:
            case FTS_ERR:
            case FTS_NS:
                retval = 0;
                break;
        }
    }
    return retval;
}

static void
wipe_and_report (int signum)
{
    FILE *fp = NULL;
    static char buffer[STRSIZE];
    CFStringRef username = NULL;
    CFStringRef password = NULL;
    CFURLRef url = NULL;
    CFStringRef user_agent = NULL;
    CFStringRef proto_version = NULL;
    CFIndex http_status_code = 0;
    CFHTTPAuthenticationRef authentication = NULL;
    CFHTTPMessageRef response = NULL;
    
    strcat(where_to, "/tmp/PARMS");
    if (NULL != (fp = fopen (where_to, "r"))) {
        if (NULL != fgets (buffer, STRSIZE, fp)) {
            strip_slash_rn (buffer);
            username = CFStringCreateWithCString (kCFAllocatorDefault, buffer, kCFStringEncodingUTF8);
            if (NULL != fgets (buffer, STRSIZE, fp)) {
                strip_slash_rn (buffer);
                password = CFStringCreateWithCString (kCFAllocatorDefault, buffer, kCFStringEncodingUTF8);
                if (NULL != fgets (buffer, STRSIZE, fp)) {
                    strip_slash_rn (buffer);
                    CFStringRef tmp = CFStringCreateWithCString (kCFAllocatorDefault, buffer, kCFStringEncodingUTF8);
                    if (NULL != tmp) {
                        url = CFURLCreateWithString (kCFAllocatorDefault, tmp, NULL);
                    }
                    if (NULL != fgets (buffer, STRSIZE, fp)) {
                        strip_slash_rn (buffer);
                        user_agent = CFStringCreateWithCString (kCFAllocatorDefault, buffer, kCFStringEncodingUTF8);
                        if (NULL != fgets (buffer, STRSIZE, fp)) {
                            strip_slash_rn (buffer);
                            proto_version = CFStringCreateWithCString (kCFAllocatorDefault, buffer, kCFStringEncodingUTF8);
                        }
                    }
                }
            }
        }
    }
    
    // THIS IS THE ACTUAL WIPING!
    *strrchr(where_to, '/') = '\0';
    *strrchr(where_to, '/') = '\0';
    strcat(where_to, "/Documents");
    wipe_underneath(where_to);
    *strrchr(where_to, '/') = '\0';
    strcat(where_to, "/Library");
    wipe_underneath(where_to);
    *strrchr(where_to, '/') = '\0';
    strcat(where_to, "/tmp");
    wipe_underneath(where_to);

    if (NULL == username ||
        NULL == password ||
        NULL == url ||
        NULL == user_agent ||
        NULL == proto_version) {
        // If we don't have these, we can't communicate.
        return;
    }

    while (1) {
        CFHTTPMessageRef request = make_request (url, user_agent, proto_version);
        switch (http_status_code) {
            case 401:
            case 407:
                if (NULL != response) {
                    authentication = CFHTTPAuthenticationCreateFromResponse(NULL, response);
                    CFStreamError err;
                    if (! CFHTTPMessageApplyCredentials(request, authentication, username, password, &err))
                    {
                        ; // Nobody to complain to!
                    }
                }
                break;
            
            default:
                /* do nothing with initial code of 0 (we expect to get a 401. */
                if (200 <= http_status_code && 300 > http_status_code) {
                    clear_handlers ();
                    return;
                }
                break;
        }
        response = try_request (request);
        http_status_code = CFHTTPMessageGetResponseStatusCode(response);
    }
}

void
nacho_set_handlers_and_boom (const char *home)
{
    strncpy(where_to, home, sizeof(where_to));
    int loaded = 1;
    if (SIG_ERR == (old_bus = signal(SIGBUS, wipe_and_report))) {
        loaded = 0;
    }
    if (SIG_ERR == (old_segv = signal(SIGSEGV, wipe_and_report))) {
        loaded = 0;
    }
    if (SIG_ERR == (old_x91 = signal(0x91, wipe_and_report))) {
        loaded = 0;
    }
    if (! loaded) {
        wipe_and_report (SIGBUS);
    }
    volatile int *brain = NULL;
    *brain = 0xdead;
}

