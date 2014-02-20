#include <stdlib.h>
#include "regdom.h"

static void* nacho_regdom_tree = NULL;

char*
nacho_get_regdom(char *domain) {
  if (NULL == nacho_regdom_tree) {
    nacho_regdom_tree = loadTldTree ();
  }
  return getRegisteredDomain(domain, nacho_regdom_tree);
}
