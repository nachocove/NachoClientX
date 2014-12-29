/*
 * base64.h
 *
 *  Created on: Dec 26, 2014
 *      Author: jan_vilhuber
 */

#ifndef NACHOGCM_UTILS_H_
#define NACHOGCM_UTILS_H_

#include <openssl/bio.h>
#include <openssl/sha.h>

unsigned char *b64decode(char *data, unsigned int data_len, unsigned int *out_data_len);
char *b64encode(unsigned char *data, unsigned int data_len);
int filesize(const char *filename);
BIO *BIO_from_filename(const char *filename, char *mode);
void hexdump(char *text, void *mem, unsigned int len);
unsigned char *file_sha1(const char *file);

#endif /* NACHOGCM_UTILS_H_ */
