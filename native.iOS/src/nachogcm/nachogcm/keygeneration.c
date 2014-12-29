/*
 * keygeneration.c
 *
 *  Created on: Dec 26, 2014
 *      Author: jan_vilhuber
 */

#include "keygeneration.h"

#include <openssl/err.h>
#include <openssl/rand.h>

#include <stdio.h>
#include <stdlib.h>

unsigned char *make_key(int bits) {
	unsigned char *key = malloc(bits/8);
	if (make_key_into_buffer(bits, key) <= 0) {
		free(key);
		key = NULL;
	}
	return key;
}

unsigned int make_key_into_buffer(int bits, unsigned char *buffer) {
	if (!RAND_bytes(buffer, bits/8)) {
		fprintf(stderr, "Could not get random bytes\n");
		ERR_print_errors_fp(stderr);
		return -1;
	}
	return (bits/8);
}
