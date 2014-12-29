/*
 * gcm_encrypt.c
 *
 *  Created on: Dec 23, 2014
 *      Author: jan_vilhuber
 */

#include "encrypt.h"
#include "iv.h"
#include "utils.h"

#include <openssl/bio.h>
#include <openssl/evp.h>
#include <openssl/buffer.h>

#include <stdio.h>
#include <stdlib.h>
#include <string.h>

int BIO_encrypt(EVP_CIPHER_CTX *ctx,
		BIO *ciphertext_bio,                // BIO to write the encrypted data to
		unsigned char *inbuf, int in_len,   // Data to encrypt
		unsigned char *outbuf, int out_len  // Used for speed, i.e. the caller provides a reusable temporary buffer.
		) {
	int rv;

	memset(outbuf, 0x0, out_len);
	rv = EVP_EncryptUpdate(ctx, outbuf, &out_len, inbuf, in_len);
	if (rv < 0) {
		return rv;
	}

	rv = BIO_write(ciphertext_bio, outbuf, out_len);
	return rv;
}

int gcm_encrypt_bio(const EVP_CIPHER *cipher,
		unsigned char *key,
		unsigned char *iv, int iv_len,
		unsigned char *aad, int aad_len, int tag_len,
		BIO *cleartext_bio, unsigned long cleartext_size,
		BIO *ciphertext_bio) {
	int rv;
	int out_len, in_len;
	unsigned char *outbuf = NULL;
	unsigned char *inbuf = NULL;
	int blocksize = 1024;

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
	rv = EVP_EncryptInit_ex(ctx, cipher, NULL, NULL, NULL);
	if (rv < 0) {
		goto cleanup;
	}
	// convert iv_len to bytes
	iv_len /= 8;
	rv = EVP_CIPHER_CTX_ctrl(ctx, EVP_CTRL_GCM_SET_IVLEN, (iv_len), NULL);
	if (rv < 0) {
		goto cleanup;
	}
	rv = EVP_EncryptInit_ex(ctx, NULL, NULL, key, iv);
	if (rv < 0) {
		goto cleanup;
	}

	/* Create the file header that tells the decryptor the parameters to use */
	in_len = snprintf((char *)inbuf, blocksize, "ivlen=%d,taglen=%d\n", iv_len*8, tag_len);
	rv = BIO_write(ciphertext_bio, inbuf, in_len);
	if (rv < 0) {
		goto cleanup;
	}
	// put the header through the authenticator
	rv = EVP_EncryptUpdate(ctx, NULL, &out_len, inbuf, in_len);
	if (rv < 0 || in_len != out_len) {
		goto cleanup;
	}
	/* Zero or more calls to specify any AAD */
	if (aad) {
		rv = EVP_EncryptUpdate(ctx, NULL, &out_len, aad, aad_len);
		if (rv < 0) {
			goto cleanup;
		}
	}
	// write the IV
	rv = BIO_write(ciphertext_bio, iv, iv_len);
	if (rv <= 0) {
		goto cleanup;
	}

	/* Create the header (which will be encrypted) that tells the decryptor information about the plaintext */
	in_len = snprintf((char *)inbuf, blocksize, "datalen=%ld\n", cleartext_size);
	rv = BIO_encrypt(ctx, ciphertext_bio, inbuf, in_len, outbuf, blocksize);
	if (rv < 0) {
		goto cleanup;
	}

	/* Encrypt plaintext */
	while ((in_len = BIO_read(cleartext_bio, inbuf, blocksize)) > 0) {
		rv = BIO_encrypt(ctx, ciphertext_bio, inbuf, in_len, outbuf, blocksize);
		if (rv < 0) {
			goto cleanup;
		}
		if (in_len < blocksize && BIO_eof(cleartext_bio)) {
			break;
		}
	}
	if (rv < 0) {
		goto cleanup;
	}

	memset(outbuf, 0x0, blocksize);
	/* Finalise: note get no output for GCM */
	rv = EVP_EncryptFinal_ex(ctx, outbuf, &out_len);
	if (rv < 0) {
		goto cleanup;
	}

	/* Get tag */
	memset(outbuf, 0x0, blocksize);
	rv = EVP_CIPHER_CTX_ctrl(ctx, EVP_CTRL_GCM_GET_TAG, tag_len, outbuf);
	if (rv < 0) {
		goto cleanup;
	}


	rv = BIO_write(ciphertext_bio, outbuf, tag_len);
	if (rv < 0) {
		goto cleanup;
	}
	// set success return value, then drop to cleanup.
	rv = 1;

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
 * Perform AES 256 GCM encryption and authentication
 *
 * In addition to doing a straight AES GCM encryption and authentication, we PREPEND a header
 * "datalen=<the integer length of the data>\n" to the ciphertext (i.e. it will be encrypted).
 * Upon decryption, the decryptor will use this to truncate any encryption padding that may come about
 * because of the fact we're using a blockcipher.
 *
 * Also, we add an authenticated header to the file (and pass it to the authenticated portion of AES-GCM)
 * "ivlen=<integer>,taglen=<integer>\n". The decryptor needs to know this so it can read the proper IV from
 * the start of the ciphertext, and skip the tag at the end (and feed it to the authentication checker).
 *
 * @param key the aes 256 key
 * @param iv the aes iv
 * @param iv_len the length of the iv in BITS
 * @param aad the authenticated data (if any)
 * @param aad_len the length of the authenticated data (if any) in bytes
 * @param tag_len The length of the AES GCM output/authentication tag in bytes
 * @param cleartext_bio The contents to encrypt aka the plaintext
 * @param cleartext_size The size of the plaintext. Some BIO's can't divulge their size, so we put the onus on the caller. in bytes
 * @param ciphertext_bio the output bio, i.e. where we write the ciphertext.
 *
 */
int aes_256_gcm_encrypt_bio(
		unsigned char *key,
		unsigned char *iv, int iv_len,
		unsigned char *aad, int aad_len, int tag_len,
		BIO *cleartext_bio, unsigned long cleartext_size,
		BIO *ciphertext_bio) {
	return gcm_encrypt_bio(EVP_aes_256_gcm(),
			key,
			iv, iv_len,
			aad, aad_len, tag_len,
			cleartext_bio, cleartext_size,
			ciphertext_bio);
}

int aes_256_gcm_encrypt_file(const char *in_filename, const char *out_filename,
		const char *device_id, long counter,
		unsigned char *key)
{
	int rv = -1;
	unsigned char *iv = NULL;
	BIO *in_bio = NULL;
	BIO *out_bio = NULL;

	iv = create_iv(device_id, counter, IV_LEN);
	if (!iv) {
		fprintf(stderr, "Could not encrypt file.\n");
		goto cleanup;
	}
	in_bio = BIO_from_filename(in_filename, "r");
	if (!in_bio) {
		fprintf(stderr, "Could not encrypt file.\n");
		goto cleanup;
	}
	out_bio = BIO_from_filename(out_filename, "w");
	if (!out_bio) {
		fprintf(stderr, "Could not encrypt file.\n");
		goto cleanup;
	}
	int in_length = filesize(in_filename);

	rv = aes_256_gcm_encrypt_bio(key, iv, 512, NULL, 0, 16, in_bio, in_length, out_bio);
	if (rv <= 0) {
		fprintf(stderr, "Could not encrypt file.\n");
		goto cleanup;
	}
cleanup:
	if (iv) free(iv);
	if (out_bio) BIO_free(out_bio);
	if (in_bio)	BIO_free(in_bio);
	return rv;
}

int aes_256_gcm_encrypt_memory(unsigned char *in_memory, unsigned int in_len,
		unsigned char **out_memory, unsigned int *out_len,
		const char *device_id, long counter,
		unsigned char *key)
{
	int rv = -1;
	unsigned char *iv = NULL;
	BIO *in_bio = NULL;
	BIO *out_bio = NULL;
	BUF_MEM *bptr = NULL;

	iv = create_iv(device_id, counter, IV_LEN);
	if (!iv) {
		fprintf(stderr, "Could not encrypt file.\n");
		goto cleanup;
	}
	in_bio = BIO_new_mem_buf(in_memory, in_len);
	if (!in_bio) {
		fprintf(stderr, "Could not encrypt file.\n");
		goto cleanup;
	}
	out_bio = BIO_new(BIO_s_mem());
	if (!out_bio) {
		fprintf(stderr, "Could not encrypt file.\n");
		goto cleanup;
	}

	rv = aes_256_gcm_encrypt_bio(key, iv, IV_LEN, NULL, 0, 16, in_bio, in_len, out_bio);
	if (rv <= 0) {
		fprintf(stderr, "Could not encrypt file.\n");
		goto cleanup;
	}

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
