.PHONY: test all clean nachogcmlib

CFLAGS = -Wall -Werror -I/usr/local/include -I ../include -I/opt/local/include 

TEST_SRCS = $(wildcard *.c)
TEST_OBJS = $(TEST_SRCS:.c=.o)
TEST_EXE = check_nachogcm
NACHOGCM_LIB = ../lib/nachogcm.a
LIBS = -L/usr/local/ssl/lib -L/usr/local/lib -L/opt/local/lib -lcheck -lcrypto

all: $(TEST_EXE)

test: all
	./$(TEST_EXE)
	
$(TEST_EXE): $(TEST_OBJS) $(NACHOGCM_LIB)
	$(CC) -o $@ $+ $(LIBS)

$(NACHOGCM_LIB):
	$(MAKE) -C ../nachogcm install
	
%.o: %.c
	$(CC) -g -c -o $@ $< $(CFLAGS)

clean:
	@rm -f $(TEST_OBJS) $(TEST_EXE) 2> /dev/null
		

