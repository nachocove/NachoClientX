#include <stdlib.h>
#include <sys/types.h>
#include <sys/sysctl.h>

#include "TargetConditionals.h"

char*
nacho_sysctlbyname(char *name) {
    size_t size;
    sysctlbyname(name, NULL, &size, NULL, 0);
    char *result = malloc(size);
    if (NULL == result) {
        return NULL;
    }
    sysctlbyname(name, result, &size, NULL, 0);
    return result;
}

unsigned
nacho_is_simulator() {
    return TARGET_IPHONE_SIMULATOR;
}
