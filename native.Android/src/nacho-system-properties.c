#include <stdlib.h>
#include <sys/system_properties.h>

char*
nacho_get_nameserver () {
  char *value = malloc(PROP_VALUE_MAX);
    int retcode = __system_property_get("net.dns1", value);
    if (! retcode) {
        free (value);
        return NULL;
    }
    return value;
}
