/*
 * base64.c
 *
 *  Created on: Dec 26, 2014
 *      Author: jan_vilhuber
 */

#include <openssl/bio.h>
#include <openssl/err.h>
#include <openssl/evp.h>
#include <openssl/buffer.h>

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/stat.h>
#include <ctype.h>

unsigned char *b64decode(char *data, unsigned int data_len, unsigned int *out_data_len) {
	BIO *b64 = BIO_new(BIO_f_base64());
	BIO *bio = BIO_new_mem_buf(data, data_len);
	BIO *out = BIO_new(BIO_s_mem());
	char inbuf[512];
	int inlen;
	int rv;
	BUF_MEM *buf;
	bio = BIO_push(b64, bio);
	BIO_set_flags(bio, BIO_FLAGS_BASE64_NO_NL);
	while((inlen = BIO_read(bio, inbuf, 512)) > 0) {
		rv = BIO_write(out, inbuf, inlen);
		if (rv < 0) {
			ERR_print_errors_fp(stderr);
			exit(1);
		}
	}
	BIO_flush(out);
	BIO_free_all(bio);
	BIO_get_mem_ptr(out, &buf);
	unsigned char *outdata = malloc(buf->length+1);
	if (!outdata) {
		perror("malloc");
		exit(1);
	}
	memset(outdata, 0x0, buf->length);
	memcpy(outdata, buf->data, buf->length);
	if (out_data_len) *out_data_len = buf->length;
	BIO_free(out);
	return outdata;

}
char *b64encode(unsigned char *data, unsigned int data_len) {
	BIO *b64 = BIO_new(BIO_f_base64());
	BIO *bio = BIO_new(BIO_s_mem());
	BUF_MEM *buf;
	BIO_push(b64, bio);
	BIO_write(b64, data, data_len);
	BIO_flush(b64);

	BIO_get_mem_ptr(b64, &buf);
	char *outdata = malloc(buf->length+1);
	if (!outdata) {
		perror("malloc");
		exit(1);
	}
	memset(outdata, 0x0, buf->length);
	memcpy(outdata, buf->data, buf->length-1);
	BIO_free_all(b64);
	return outdata;

}

int filesize(const char *filename) {
	struct stat st;
	stat(filename, &st);
	return (int)st.st_size;
}

BIO *BIO_from_filename(const char *filename, char *mode) {
	if (!filename) {
		return NULL;
	}
	if ((strlen(filename) == 1) && (filename[0] == '-')) {
		return BIO_new_fp(mode[0] == 'w' ? stdout: stdin, BIO_NOCLOSE);
	}

	FILE *fp = fopen(filename, mode);
	return BIO_new_fp(fp, BIO_CLOSE);
}


#define HEXDUMP_COLS 16
void hexdump(char *text, void *mem, unsigned int len)
{
        unsigned int i, j;

        printf("%s (%d):\n", text, len);
        for(i = 0; i < len + ((len % HEXDUMP_COLS) ? (HEXDUMP_COLS - len % HEXDUMP_COLS) : 0); i++)
        {
                /* print offset */
                if(i % HEXDUMP_COLS == 0)
                {
                        printf("0x%06x: ", i);
                }

                /* print hex data */
                if(i < len)
                {
                        printf("%02x ", 0xFF & ((char*)mem)[i]);
                }
                else /* end of block, just aligning for ASCII dump */
                {
                        printf("   ");
                }

                /* print ASCII dump */
                if(i % HEXDUMP_COLS == (HEXDUMP_COLS - 1))
                {
                        for(j = i - (HEXDUMP_COLS - 1); j <= i; j++)
                        {
                                if(j >= len) /* end of block, not really printing */
                                {
                                        putchar(' ');
                                }
                                else if(isprint(((char*)mem)[j])) /* printable char */
                                {
                                        putchar(0xFF & ((char*)mem)[j]);
                                }
                                else /* other char */
                                {
                                        putchar('.');
                                }
                        }
                        putchar('\n');
                }
        }
}
