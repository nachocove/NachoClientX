/*
 * gcm_decrypt.c
 *
 *  Created on: Dec 24, 2014
 *      Author: jan_vilhuber
 */

#include "decrypt.h"
#include "utils.h"

#include <openssl/bio.h>
#include <openssl/evp.h>
#include <openssl/buffer.h>

#include <stdio.h>
#include <stdlib.h>
#include <string.h>

int BIO_decrypt(EVP_CIPHER_CTX *ctx,
		BIO *cleartext_bio,                // BIO to write the decrypted data to
		unsigned char *outbuf, int out_len,  // Used for speed, i.e. the caller provides a reusable temporary buffer.
		unsigned char *inbuf, int in_len,   // Data to decrypt
		int max_write
) {
	int rv;

	memset(outbuf, 0x0, out_len);
	rv = EVP_DecryptUpdate(ctx, outbuf, &out_len, inbuf, in_len);
	if (rv < 0) {
		return rv;
	}
	if (max_write == -1) {
		max_write = out_len;
	}
	rv = BIO_write(cleartext_bio, outbuf, max_write);
	return rv;
}

long gcm_decrypt_bio(const EVP_CIPHER *cipher,
		unsigned char *key,
		unsigned char *aad, int aad_len,
		BIO *ciphertext_bio,
		BIO *cleartext_bio) {
	int rv;
	int out_len, in_len;
	unsigned char *outbuf = NULL;
	unsigned char *inbuf = NULL;
	unsigned long blocksize = 1024;
	int tag_len, iv_len;
	long datalen;

	EVP_CIPHER_CTX *ctx = EVP_CIPHER_CTX_new();
	if (!ctx) {
		rv = 1;
		goto cleanup;
	}
	outbuf = malloc(blocksize);
	inbuf = malloc(blocksize);
	if (!outbuf || !inbuf) {
		rv = -1;
		// TODO should add an openssl error here
		goto cleanup;
	}
	EVP_CIPHER_CTX_init(ctx);
	rv = EVP_DecryptInit_ex(ctx, cipher, NULL, NULL, NULL);
	if (rv < 0) {
		goto cleanup;
	}

	// read the first line from the file to get the iv and tag length
	in_len = BIO_gets(ciphertext_bio, (char *)inbuf, blocksize);
	if (in_len < 0) {
		goto cleanup;
	}
	if (sscanf((char *)inbuf, "ivlen=%d,taglen=%d\n", &iv_len, &tag_len) <= 0) {
		rv = -1;
		goto cleanup;
	}
	if (iv_len<96) {
		fprintf(stderr, "iv_len in file is too small: %d\n", iv_len);
		goto cleanup;
	}
	// convert iv_len to bytes
	iv_len /= 8;
	rv = EVP_CIPHER_CTX_ctrl(ctx, EVP_CTRL_GCM_SET_IVLEN, iv_len, NULL);
	if (rv < 0) {
		goto cleanup;
	}
	// get the IV
	{
		unsigned char *iv = malloc(iv_len);
		if (!iv) {
			rv = -1;
			goto cleanup;
		}
		memset(iv, 0x0, iv_len);
		rv = BIO_read(ciphertext_bio, iv, iv_len);
		if (rv < 0) {
			goto cleanup;
		}

		// now that we have the IV, initialize the decryptor state
		rv = EVP_DecryptInit_ex(ctx, NULL, NULL, key, iv);
		if (rv < 0) {
			goto cleanup;
		}
	}

	// feed the header in as AAD, because it's part of the authenticated data
	rv = EVP_DecryptUpdate(ctx, NULL, &out_len, inbuf, in_len);
	if (in_len != out_len) {
		rv = -1;
	}
	if (rv < 0) {
		goto cleanup;
	}

	if (aad) {
		/* Zero or more calls to specify any AAD */
		rv = EVP_DecryptUpdate(ctx, NULL, &out_len, aad, aad_len);
		if (rv < 0) {
			goto cleanup;
		}
	}

	// get some data. 30 bytes should cover the "datalen=NNNN\n" string.
	// Whatever is not the then write the rest to the bio, since it's now decrypted.
	memset(inbuf, 0x0, blocksize);
	in_len = BIO_read(ciphertext_bio, inbuf, 30);
	if (in_len < 0) {
		rv = in_len;
		goto cleanup;
	}
	memset(outbuf, 0x0, blocksize);
	rv = EVP_DecryptUpdate(ctx, outbuf, &out_len, inbuf, in_len);
	if (rv < 0) {
		goto cleanup;
	}
	if (sscanf((char *)outbuf, "datalen=%ld\n", &datalen) <= 0) {
		fprintf(stderr, "Could not find header in ciphertext");
		rv = -1;
		goto cleanup;
	}
	unsigned char *cp = outbuf;
	while (*(cp++) != '\n') {
		out_len--;
	}
	out_len--;

	// write the rest of the plaintext that is not header to the bio
	rv = BIO_write(cleartext_bio, cp, out_len);
	if (rv < 0) {
		goto cleanup;
	}

	// TODO This seems like a bug waiting to happen. The assumption here is that
	// amount of encrypted data is exactly the same as amount of plaintext data.
	// That makes me nervous.
	unsigned long clear_data_left = datalen - rv;

	/* Decrypt the rest */
	while ((in_len = BIO_read(ciphertext_bio, inbuf, clear_data_left)) > 0) {
		rv = BIO_decrypt(ctx, cleartext_bio,
				outbuf, out_len,
				inbuf, in_len,
				blocksize > clear_data_left ? clear_data_left : blocksize);
		if (rv < 0) {
			goto cleanup;
		}
		clear_data_left -= rv;
		if (clear_data_left > 0 && BIO_eof(cleartext_bio)) {
			break;
		}
	}

	memset(inbuf, 0x0, blocksize);
	rv = BIO_read(ciphertext_bio, inbuf, tag_len);
	if (rv < 0) {
		goto cleanup;
	}
	if (rv != tag_len) {
		rv = -1;
		goto cleanup;
	}
	rv = EVP_CIPHER_CTX_ctrl(ctx, EVP_CTRL_GCM_SET_TAG, tag_len, inbuf);
	if (rv < 0) {
		goto cleanup;
	}

	/* Finalise: note get no output for GCM */
	memset(inbuf, 0x0, blocksize);
	in_len = 0;
	rv = EVP_DecryptFinal_ex(ctx, inbuf, &in_len);
	if (rv <= 0) {
		fprintf(stderr, "Tag did not validate\n");
		rv = -1;
		goto cleanup;
	}
	rv = datalen;

cleanup:
	/*
	 * Note, if you fail to cleanup, there will be a memory leak.
	 */
	EVP_CIPHER_CTX_cleanup(ctx);
	EVP_CIPHER_CTX_free(ctx);
	if (outbuf) free(outbuf);
	if (inbuf) free(inbuf);
	return rv;
}

/*
 * Perform AES 256 GCM decryption and authentication
 *
 * Read the given input, parsing the various headers along the way. Return only the decrypted
 * user data without the headers.
 *
 * @param key the aes 256 key
 * @param iv the aes iv
 * @param iv_len the length of the iv in BITS
 * @param aad the authenticated data (if any)
 * @param aad_len the length of the authenticated data (if any) in bytes
 * @param tag_len The length of the AES GCM output/authentication tag in bytes
 * @param cleartext_bio the output bio, i.e. where we write the plaintext
 * @param ciphertext_bio The source of the ciphertext.
 *
 * @return The size of the plaintext.
 */
long aes_256_gcm_decrypt_bio(
		unsigned char *key,
		unsigned char *aad, int aad_len,
		BIO *ciphertext_bio,
		BIO *cleartext_bio) {
	return gcm_decrypt_bio(EVP_aes_256_gcm(),
			key,
			aad, aad_len,
			ciphertext_bio,
			cleartext_bio);
}

int aes_256_gcm_decrypt_file(const char *in_filename, const char *out_filename,
		unsigned char *key)
{
	int rv = -1;
	unsigned char *iv = NULL;
	BIO *in_bio = NULL;
	BIO *out_bio = NULL;

	in_bio = BIO_from_filename(in_filename, "r");
	if (!in_bio) {
		fprintf(stderr, "Could not create file bio file from %s.\n", in_filename);
		goto cleanup;
	}
	out_bio = BIO_from_filename(out_filename, "w");
	if (!out_bio) {
		fprintf(stderr, "Could not create file bio file from %s.\n", in_filename);
		goto cleanup;
	}

	rv = gcm_decrypt_bio(EVP_aes_256_gcm(),
			key,
			NULL, 0,
			in_bio,
			out_bio);
	if (rv < 0) {
		goto cleanup;
	}

cleanup:
	if (iv) free(iv);
	if (out_bio) BIO_free(out_bio);
	if (in_bio)	BIO_free(in_bio);
	return rv;
}

int aes_256_gcm_decrypt_memory(unsigned char *in_memory, unsigned int in_len,
		unsigned char **out_memory, unsigned int *out_len,
		unsigned char *key)
{
	int rv = -1;
	unsigned char *iv = NULL;
	BIO *in_bio = NULL;
	BIO *out_bio = NULL;
	BUF_MEM *bptr = NULL;

	in_bio = BIO_new_mem_buf(in_memory, in_len);
	if (!in_bio) {
		fprintf(stderr, "Could not create memory bio.\n");
		goto cleanup;
	}
	out_bio = BIO_new(BIO_s_mem());
	if (!out_bio) {
		fprintf(stderr, "Could not create memory bio.\n");
		goto cleanup;
	}

	rv = gcm_decrypt_bio(EVP_aes_256_gcm(),
			key,
			NULL, 0,
			in_bio,
			out_bio);

	rv = BIO_get_mem_ptr(out_bio, &bptr);
	if (rv < 0) {
		goto cleanup;
	}

	unsigned char *outdata = malloc(bptr->length);
	if (!outdata) {
		rv = -1;
		goto cleanup;
	}
	*out_memory = outdata;
	*out_len = bptr->length;
	memcpy(outdata, bptr->data, bptr->length);
	rv = bptr->length;

cleanup:
	if (iv) free(iv);
	if (out_bio) BIO_free(out_bio);
	if (in_bio)	BIO_free(in_bio);
	return rv;

}
