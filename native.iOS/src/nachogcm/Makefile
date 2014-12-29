CFLAGS = -Wno-deprecated-declarations -Werror -Wall -Wextra -I/usr/local/ssl/include -I./include -g -O0

NACHOGCM_LIB=lib/nachogcm.a
LIBS = $(NACHOGCM_LIB) -L/usr/local/ssl/lib 
.PHONY: clean all test nachogcm tests

all: gcm_test tests

nachogcm:
	$(MAKE) -C nachogcm install

gcm_test: gcm_test.o nachogcm
	$(CC) -o $@ $< $(LIBS) -lcrypto

%.o: %.c
	$(CC) -g -c -o $@ $< $(CFLAGS)

clean:
	@$(MAKE) -C nachogcm clean
	@$(MAKE) -C tests clean
	@rm -f gcm_test gcm_test.o lib/* include/nachogcm/* 2> /dev/null

tests:
	$(MAKE) -C tests all

test:
	$(MAKE) -C tests test
