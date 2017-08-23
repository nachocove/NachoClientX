#include <stdlib.h>
#include <string.h>
#include "regdom.h"

static void* nacho_regdom_tree = NULL;

void
nacho_get_regdom(char *dest, size_t limit, char *domain) {
  if (NULL == nacho_regdom_tree) {
    nacho_regdom_tree = loadTldTree ();
  }
  char *src = getRegisteredDomain(domain, nacho_regdom_tree);
  if (NULL != src) {
    strncpy (dest, src, limit);
  }
}

