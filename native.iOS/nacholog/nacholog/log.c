//
//  log.c
//  NachoPlatform
//
//  Created by Owen Shaw on 6/20/17.
//  Copyright © 2017 Nacho Cove, Inc. All rights reserved.
//

#include <os/log.h>

os_log_t nacho_os_log_create(const char *subsystem, const char *category){
    return os_log_create(subsystem, category);
}

void nacho_os_log_debug(os_log_t log, const char *message){
    os_log_debug(log, "%{public}s", message);
}

void nacho_os_log_info(os_log_t log, const char *message){
    os_log_info(log, "%{public}s", message);
}

void nacho_os_log_warn(os_log_t log, const char *message){
    os_log_error(log, "%{public}s", message);
}

void nacho_os_log_error(os_log_t log, const char *message){
    os_log_fault(log, "%{public}s", message);
}
