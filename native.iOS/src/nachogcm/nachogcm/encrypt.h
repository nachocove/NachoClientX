/*
 * gcm_encrypt.h
 *
 *  Created on: Dec 23, 2014
 *      Author: jan_vilhuber
 */

#ifndef GCM_ENCRYPT_H_
#define GCM_ENCRYPT_H_

#include <openssl/bio.h>

#define IV_LEN 512

int aes_256_gcm_encrypt_bio(
		unsigned char *key,
		unsigned char *iv, int iv_len,
		unsigned char *aad, int aad_len, int tag_len,
		BIO *cleartext_bio, unsigned long cleartext_size,
		BIO *ciphertext_bio);

int aes_256_gcm_encrypt_file(const char *in_filename, const char *out_filename,
		const char *device_id, long counter,
		unsigned char *key);

int aes_256_gcm_encrypt_memory(unsigned char *in_memory, unsigned int in_len,
		unsigned char **out_memory, unsigned int *out_len,
		const char *device_id, long counter,
		unsigned char *key);

#endif /* GCM_ENCRYPT_H_ */
