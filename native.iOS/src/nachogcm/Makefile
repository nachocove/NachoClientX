OPENSSL_HOME ?= /usr/local/ssl
CFLAGS = -Wno-deprecated-declarations -Werror -Wall -Wextra -I$(OPENSSL_HOME)/include -I./include -I/opt/local/include -g -O0 

NACHOGCM_LIB=lib/nachogcm.a
LIBS = $(NACHOGCM_LIB) -L$(OPENSSL_HOME)/lib -lcrypto
.PHONY: clean all test nachogcm tests

all: gcm_test tests

nachogcm:
	@if [ ! -d $(OPENSSL_HOME)/include ] ; then echo "OPENSSL_HOME=$(OPENSSL_HOME) is not valid"; exit 1; fi
	$(MAKE) -C nachogcm install

gcm_test: gcm_test.o nachogcm
	$(CC) -o $@ $< $(LIBS)

%.o: %.c
	$(CC) -g -c -o $@ $< $(CFLAGS)

clean:
	@$(MAKE) -C nachogcm clean
	@$(MAKE) -C tests clean
	@rm -f gcm_test gcm_test.o lib/* include/nachogcm/* 2> /dev/null

tests: nachogcm
	$(MAKE) -C tests all

test: tests
	$(MAKE) -C tests test
