/*
 * gcm_test.c
 *
 *  Created on: Dec 23, 2014
 *      Author: jan_vilhuber
 */

#include <assert.h>
#include <getopt.h>
#include <openssl/bio.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#include "nachogcm/decrypt.h"
#include "nachogcm/encrypt.h"
#include "nachogcm/iv.h"
#include "nachogcm/keygeneration.h"
#include "nachogcm/utils.h"

#define AES_256_BIT_KEY_SIZE 32
#define BITS 8

void print_usage(char *progname)
{
	printf("USAGE:\n");
	printf("\t%s -m|--makekey (output a valid AES key to stdout)\n", progname);
	printf("\t%s -i|--ivtest <an iv> (test a given IV for correctness)\n", progname);
	printf("\t%s -k|--key <key> -e|--encrypt [-o|--outfile <output filename>] <filename> (encrypt a file, optionally into another file (stdout by default)\n", progname);
	printf("\t%s -k|--key <key> -d|--decrypt [-o|--outfile <output filename>] <filename> (decrypt a file, optionally into another file (stdout by default)\n", progname);
	printf("\t%s -h|--help\n", progname);
}
int main(int argc, char **argv) {
	char *device_id = "Ncho3168E1E2XF59EX4E37XAFDEX";
	unsigned long counter = 1234;
	int iv_len = 0;
	char *input_file = "-";
	char *output_file = "-";
	unsigned char *key = NULL;
	int do_encrypt = 0;
	int do_decrypt = 0;


	static struct option long_options[] = {
			{"ivtest", required_argument, 0,  'i' },
			{"key",     required_argument, 0,  'k' },
			{"makekey", no_argument,       0,  'm' },
			{"encrypt", required_argument, 0,  'e' },
			{"decrypt", required_argument, 0,  'd' },
			{"outfile", required_argument, 0,  'o' },
			{"help",    no_argument,       0,  'h' },
			{0,         0,                 0,   0  }
	};
	int num_opts = sizeof(long_options)/sizeof(struct option);
	char short_args[num_opts*2];
	memset(short_args, 0x0, num_opts*2);
	{
		char *cp = short_args;
		int i;
		for (i=0;i<num_opts;i++) {
			if (long_options[i].flag != 0) continue;
			if ((long_options[i].val >= 'a' && long_options[i].val <= 'z') ||
					(long_options[i].val >= 'A' && long_options[i].val <= 'Z') ||
					(long_options[i].val >= '0' && long_options[i].val <= '9')) {
				*cp++ = long_options[i].val;

				if (long_options[i].has_arg == required_argument) {
					*cp++ = ':';
				}
			}
		}
	}
	int c;
	while ((c = getopt_long(argc, argv, short_args, long_options, NULL)) != -1) {
		switch (c) {
		case 'm':
			key = make_key((AES_256_BIT_KEY_SIZE*BITS)); // makes a 32 byte string
			printf("%s\n", b64encode(key, AES_256_BIT_KEY_SIZE));
			exit(0);

		case '?':
		case 'h':
			print_usage(argv[0]);
			break;

		case 'k':
		{
			char *x = (char *)optarg;
			unsigned int len;
			key = b64decode(x, strlen(x), &len);
			assert(len == AES_256_BIT_KEY_SIZE);
			break;
		}
		case 'i':
			iv_len = atoi(optarg);
			if (iv_len%8 != 0) {
				fprintf(stderr, "IV Length must be a multiple of 8!\n");
				exit(1);
			}
			exit(0);

		case 'd':
			if (do_encrypt || do_decrypt) {
				print_usage(argv[0]);
				exit(1);
			}
			do_decrypt = 1;
			input_file = optarg;
			break;

		case 'e':
			if (do_encrypt || do_decrypt) {
				print_usage(argv[0]);
				exit(1);
			}
			do_encrypt = 1;
			input_file = optarg;
			break;

		case 'o':
			output_file = optarg;
			break;

		default:
			printf("?? getopt returned character code 0%o ??\n", c);
		}
	}
	argc -= optind;
	argv += optind;
	if (optind < argc) {
		printf("non-option ARGV-elements: ");
		while (optind < argc)
			printf("%s ", argv[optind++]);
		printf("\n");
	}

	if (iv_len) {
		unsigned char *iv = create_iv(device_id, counter, iv_len);
		int i;
		int line_count;

		for (i=0, line_count=0; i<(iv_len/8); i++) {
			if (line_count > 0 && line_count%4 == 0) {
				printf(" ");
			}
			printf ("%02X", iv[i]);
			fflush(stdout);
			if (line_count > 0 && line_count%15 == 0) {
				printf("\n");
				line_count = 0;
			} else {
				line_count ++;
			}
		}
		printf("\n");
		free(iv);
	}
	if (do_encrypt) {
		if (!key) {
			fprintf(stderr, "ERROR: Must provide a key\n");
			exit(1);
		}
		if (aes_256_gcm_encrypt_file(input_file, output_file,
				device_id, counter,
				key) <= 0) {
			fprintf(stderr, "Could not encrypt file.\n");
			exit(1);
		}
		free(key);
	}
	if (do_decrypt) {
		if (!key) {
			fprintf(stderr, "ERROR: Must provide a key\n");
			exit(1);
		}
		FILE *fp = fopen(input_file, "r");
		BIO *ciphertext_bio = BIO_new_fp(fp, BIO_CLOSE);
		fp = NULL; // BIO takes over and will close
		BIO *plaintext_bio = BIO_new_fp(fp, BIO_CLOSE);
		if (output_file) {
			plaintext_bio = BIO_new_file(output_file, "w");
		} else {
			plaintext_bio = BIO_new_fp(stdout, BIO_NOCLOSE);
		}
		long clear_size;
		clear_size = aes_256_gcm_decrypt_bio(key, NULL, 0, ciphertext_bio, plaintext_bio);
		if (clear_size < 0) {
			fprintf(stderr, "Could not decrypt file.\n");
			exit(1);
		}
	}
}
