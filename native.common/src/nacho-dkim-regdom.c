#include <stdlib.h>
#include "tld-canon.h"
#include "dkim-regdom.h"

static tldnode* nacho_regdom_tree = NULL;

char*
nacho_get_regdom(char *domain) {
  if (NULL == nacho_regdom_tree) {
    nacho_regdom_tree = readTldTree(tldString);
  }
  return getRegisteredDomain(domain, nacho_regdom_tree);
}
