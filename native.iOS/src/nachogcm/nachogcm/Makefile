CFLAGS = -Wno-deprecated-declarations -Werror -Wall -Wextra -I/usr/local/ssl/include -I/opt/local/include 
.PHONY: clean all install
LIB_SRCS = $(wildcard *.c)
LIB_OBJS = $(LIB_SRCS:.c=.o)
LIB_HEADERS = $(LIB_SRCS:.c=.h)
NACHOGCM_LIB = nachogcm.a
AR_ARGS = -r -s -u

all: $(NACHOGCM_LIB)

$(NACHOGCM_LIB): $(LIB_OBJS)
	ar $(AR_ARGS) $@ $+
	ranlib $@
	
install: $(NACHOGCM_LIB)
	mkdir -p ../include/nachogcm/
	mkdir -p ../lib/
	cp $(LIB_HEADERS) ../include/nachogcm/
	cp $(NACHOGCM_LIB) ../lib/
	
%.o: %.c
	$(CC) -g -c -o $@ $< $(CFLAGS)

clean:
	@rm -f $(LIB_OBJS) $(NACHOGCM_LIB) 2> /dev/null

