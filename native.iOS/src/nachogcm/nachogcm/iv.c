#include "iv.h"

#include <assert.h>
#include <openssl/err.h>
#include <openssl/rand.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

unsigned char *create_iv(const char *device_id, long counter, int iv_len_bits) {
	if (iv_len_bits%8 != 0) {
		fprintf(stderr, "IV must be a multiple of 8 bits long\n");
		return NULL;
	}
	int iv_len = iv_len_bits/8;
	unsigned char *iv = (unsigned char *)malloc(iv_len);
	if (!iv) {
		fprintf(stderr, "Could not allocate space for iv\n");
		return NULL;
	}

	if (create_iv_into_buffer(device_id, counter, iv, iv_len) < 0) {
		free(iv);
		return NULL;
	}
	return iv;
}

int create_iv_into_buffer(const char *device_id, long counter, unsigned char *buffer, int iv_len) {
	int device_id_len = strlen(device_id);
	int counter_len = sizeof(counter);
	unsigned char *ivp = buffer;
	int rand_bytes = iv_len - (device_id_len + counter_len);
	assert(rand_bytes > 0);

	if (!RAND_bytes(ivp, rand_bytes)) {
		fprintf(stderr, "Could not get random bytes\n");
		ERR_print_errors_fp(stderr);
		return -1;
	}
	ivp += rand_bytes;

	memcpy(ivp, device_id, strlen(device_id));
	ivp += strlen(device_id);

	memcpy(ivp, &counter, sizeof(counter));
	ivp += sizeof(counter);
	assert(ivp == &buffer[iv_len]);
	return iv_len;
}
