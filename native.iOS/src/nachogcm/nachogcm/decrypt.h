/*
 * gcm_decrypt.h
 *
 *  Created on: Dec 24, 2014
 *      Author: jan_vilhuber
 */

#ifndef GCM_DECRYPT_H_
#define GCM_DECRYPT_H_

#include <openssl/bio.h>

long aes_256_gcm_decrypt_bio(
		unsigned char *key,
		unsigned char *aad, int aad_len,
		BIO *ciphertext_bio,
		BIO *cleartext_bio);

int aes_256_gcm_decrypt_file(const char *in_filename, const char *out_filename,
		unsigned char *key);

int aes_256_gcm_decrypt_memory(unsigned char *in_memory, unsigned int in_len,
		unsigned char **out_memory, unsigned int *out_len,
		unsigned char *key);

#endif /* GCM_DECRYPT_H_ */
