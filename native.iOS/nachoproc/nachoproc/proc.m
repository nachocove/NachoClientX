//
//  PlatformProcess.m
//  NachoPlatformLib
//
//  Created by Henry Kwok on 10/9/14.
//  Copyright (c) 2014 Nacho Cove, Inc. All rights reserved.
//

#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>
#import <sys/param.h>
#import <sys/stat.h>
#import "mach/mach.h"
#import "proc.h"
#import <netinet/in.h>
#import <arpa/inet.h>
#import <execinfo.h>

long long nacho_get_used_memory()
{
    struct task_basic_info info;
    mach_msg_type_number_t size = sizeof(info);
    kern_return_t kerr = task_info(mach_task_self(), TASK_BASIC_INFO, (task_info_t)&info, &size);
    vm_size_t process_size = (kerr == KERN_SUCCESS) ? info.resident_size : 0; // size in bytes
    return (long long)process_size;
}

int nacho_get_current_number_of_file_descriptors()
{
    struct rlimit info;
    int rc = getrlimit(RLIMIT_NOFILE, &info);
    if (0 != rc) {
        return -1;
    }
    return (int)info.rlim_cur;
}

int nacho_get_current_number_of_in_use_file_descriptors()
{
    struct stat info;
    int numFds, numInUseFds = 0;
    
    numFds = nacho_get_current_number_of_file_descriptors();
    for (int fd = 0; fd < numFds; fd++) {
        if (0 == fstat(fd, &info)) {
            numInUseFds++;
        }
    }
    return numInUseFds;
}

int nacho_get_current_in_use_file_descriptors(int* fds, int limit)
{
    struct stat info;
    int numFds;
    
    numFds = nacho_get_current_number_of_file_descriptors();

    int i = 0;
    for (int fd = 0; fd < numFds && i < limit; fd++) {
        if (0 == fstat(fd, &info)) {
            fds[i++] = fd;
        }
    }
    return i;

}

void nacho_get_filename_for_descriptor(int fd, char* buf, int limit)
{
    int rc;
    struct stat info;
    
    buf[0] = '\0';

    // Make sure that the fd exists
    if (0 != fstat(fd, &info)) {
        return;
    }
    
    if (S_ISSOCK(info.st_mode)) {
        char sock_buf[2048];
        struct sockaddr *addr = (struct sockaddr *)sock_buf;
        socklen_t addr_len = sizeof(sock_buf);
        int rc = getpeername(fd, addr, &addr_len);
        if (0 != rc) {
            strncpy(buf, "<socket: destination unknown>", limit);
        } else {
            switch (addr->sa_family) {
                case AF_INET: {
                    struct sockaddr_in *addr4 = (struct sockaddr_in *)sock_buf;
                    char addr_buf[INET_ADDRSTRLEN+1];
                    memset(addr_buf, 0, sizeof(addr_buf));
                    const char *addr_str = inet_ntop(AF_INET, &addr4->sin_addr.s_addr, addr_buf, sizeof(addr_buf));
                    if (addr_str) {
                        snprintf(buf, limit, "<ipv4 socket: %s>", addr_str);
                    } else {
                        snprintf(buf, limit, "<ipv4 socket: unknown address (errno=%d)", errno);
                    }
                    break;
                }
                case AF_INET6: {
                    struct sockaddr_in6 *addr6 = (struct sockaddr_in6 *)sock_buf;
                    char addr6_buf[INET6_ADDRSTRLEN+1];
                    memset(addr6_buf, 0, sizeof(addr6_buf));
                    const char *addr_str = inet_ntop(AF_INET6, &addr6->sin6_addr, addr6_buf, sizeof(addr6_buf));
                    if (addr_str) {
                        snprintf(buf, limit, "<ipv6 socket: %s>", addr_str);
                    } else {
                        snprintf(buf, limit, "<ipv6 socket: unknown address (errno=%d)", errno);
                    }
                    break;
                }
                case AF_UNIX: {
                    // In Xcode 6, sockaddr_un disappears. So, we need to directly
                    // print out the 2nd byte of the sockaddr where the file path starts
                    snprintf(buf, limit, "<unix socket: %s>", &sock_buf[2]);
                    break;
                }
                case AF_SYSTEM: {
                    snprintf(buf, limit, "<system socket>");
                    break;
                }
                default: {
                    snprintf(buf, limit, "<unknown socket: af=%d>", addr->sa_family);
                    break;
                }
            }
        }
    }else if (S_ISFIFO(info.st_mode)) {
        strncpy(buf, "<fifo>", limit);
    }else{
        // Get the file path
        memset(buf, sizeof(buf), 0);
        rc = fcntl(fd, F_GETPATH, buf);
        if (0 != rc) {
            strncpy(buf, "<unknown>", limit);
        }
    }
}

int nacho_get_number_of_system_threads()
{
    mach_msg_type_number_t count;
    thread_act_array_t thread_list;
    if (KERN_SUCCESS != task_threads(mach_task_self(), &thread_list, &count)) {
        return -1;
    }
    return count;
}

char** nacho_get_stack_trace()
{
    void* callstack[128];
    int numFrames = backtrace(callstack, 128);
    return backtrace_symbols(callstack, numFrames);
}
