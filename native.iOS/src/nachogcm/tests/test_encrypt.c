/*
 * test_encrypt.c
 *
 *  Created on: Dec 26, 2014
 *      Author: jan_vilhuber
 */


#include <check.h>
#include "nachogcm/keygeneration.h"
#include "nachogcm/encrypt.h"
#include "nachogcm/decrypt.h"
#include "nachogcm/utils.h"
#include <unistd.h>

START_TEST (test_encryption)
{
	char in_memory[] = "hello world";
	unsigned int in_len = strlen(in_memory);
	unsigned char *out_memory = NULL;
	unsigned int out_len = 0;
	unsigned char key[] = "fdf+PPzb3ZLg800qfIuyZYgJaIJJ03AcyhSVTGIyNOQ=";

	int rv = aes_256_gcm_encrypt_memory((unsigned char *)in_memory, in_len, &out_memory, &out_len, "Ncho12345", 12345, key);
	ck_assert_int_gt(rv, 0);
	ck_assert_ptr_ne(out_memory, 0x0);
}
END_TEST

START_TEST (test_encryption_and_decryption_lt_1_block)
{
	int in_data_len = 1024/2;
	char *in_data = malloc(in_data_len);
	unsigned char *out_memory = NULL;
	unsigned int in_len = 0;
	unsigned int out_len = 0;
	unsigned char key[] = "fdf+PPzb3ZLg800qfIuyZYgJaIJJ03AcyhSVTGIyNOQ=";
	unsigned char *in_memory = NULL;

	memset(in_data, 0x2, in_data_len);
	int rv = aes_256_gcm_encrypt_memory((unsigned char *)in_data, in_data_len, &out_memory, &out_len, "Ncho12345", 12345, key);
	ck_assert_int_gt(rv, in_data_len);
	ck_assert_ptr_ne(out_memory, 0x0);

	in_memory = out_memory;
	in_len = out_len;

	out_memory = NULL;
	out_len = 0;
	rv = aes_256_gcm_decrypt_memory(in_memory, in_len, &out_memory, &out_len, key);
	ck_assert_int_gt(rv, 0);
	ck_assert_int_eq(rv, in_data_len);
	ck_assert_ptr_ne(out_memory, 0x0);
	ck_assert(memcmp(in_data, out_memory, in_data_len) == 0);
	free(in_data);
	free(in_memory);
	free(out_memory);
}
END_TEST

START_TEST (test_encryption_and_decryption_1_block)
{
	int in_data_len = 1024;
	char *in_data = malloc(in_data_len);
	unsigned char *out_memory = NULL;
	unsigned int in_len = 0;
	unsigned int out_len = 0;
	unsigned char key[] = "fdf+PPzb3ZLg800qfIuyZYgJaIJJ03AcyhSVTGIyNOQ=";
	unsigned char *in_memory = NULL;

	memset(in_data, 0x2, in_data_len);
	int rv = aes_256_gcm_encrypt_memory((unsigned char *)in_data, in_data_len, &out_memory, &out_len, "Ncho12345", 12345, key);
	ck_assert_int_gt(rv, in_data_len);
	ck_assert_ptr_ne(out_memory, 0x0);

	in_memory = out_memory;
	in_len = out_len;

	out_memory = NULL;
	out_len = 0;
	rv = aes_256_gcm_decrypt_memory(in_memory, in_len, &out_memory, &out_len, key);
	ck_assert_int_gt(rv, 0);
	ck_assert_int_eq(rv, in_data_len);
	ck_assert_ptr_ne(out_memory, 0x0);
	ck_assert(memcmp(in_data, out_memory, in_data_len) == 0);
	free(in_data);
	free(in_memory);
	free(out_memory);
}
END_TEST

START_TEST (test_encryption_and_decryption_2_blocks)
{
	int in_data_len = 2*1024;
	char *in_data = malloc(in_data_len);
	unsigned char *out_memory = NULL;
	unsigned int in_len = 0;
	unsigned int out_len = 0;
	unsigned char key[] = "fdf+PPzb3ZLg800qfIuyZYgJaIJJ03AcyhSVTGIyNOQ=";
	unsigned char *in_memory = NULL;

	memset(in_data, 0x2, in_data_len);
	int rv = aes_256_gcm_encrypt_memory((unsigned char *)in_data, in_data_len, &out_memory, &out_len, "Ncho12345", 12345, key);
	ck_assert_int_gt(rv, in_data_len);
	ck_assert_ptr_ne(out_memory, 0x0);

	in_memory = out_memory;
	in_len = out_len;

	out_memory = NULL;
	out_len = 0;
	rv = aes_256_gcm_decrypt_memory(in_memory, in_len, &out_memory, &out_len, key);
	ck_assert_int_gt(rv, 0);
	ck_assert_int_eq(rv, in_data_len);
	ck_assert_ptr_ne(out_memory, 0x0);
	ck_assert(memcmp(in_data, out_memory, in_data_len) == 0);
	free(in_data);
	free(in_memory);
	free(out_memory);
}
END_TEST

START_TEST (test_encryption_and_decryption_1_blocks_plus)
{
	int in_data_len = 1024+1;
	char *in_data = malloc(in_data_len);
	unsigned char *out_memory = NULL;
	unsigned int in_len = 0;
	unsigned int out_len = 0;
	unsigned char key[] = "fdf+PPzb3ZLg800qfIuyZYgJaIJJ03AcyhSVTGIyNOQ=";
	unsigned char *in_memory = NULL;

	memset(in_data, 0x2, in_data_len);
	int rv = aes_256_gcm_encrypt_memory((unsigned char *)in_data, in_data_len, &out_memory, &out_len, "Ncho12345", 12345, key);
	ck_assert_int_gt(rv, in_data_len);
	ck_assert_ptr_ne(out_memory, 0x0);

	in_memory = out_memory;
	in_len = out_len;

	out_memory = NULL;
	out_len = 0;
	rv = aes_256_gcm_decrypt_memory(in_memory, in_len, &out_memory, &out_len, key);
	ck_assert_int_gt(rv, 0);
	ck_assert_int_eq(rv, in_data_len);
	ck_assert_ptr_ne(out_memory, 0x0);
	ck_assert(memcmp(in_data, out_memory, in_data_len) == 0);
	free(in_data);
	free(in_memory);
	free(out_memory);
}
END_TEST

int compare_files(const char *file1, const char *file2) {
	if (filesize(file1) != filesize(file2)) {
		return 0;
	}
	unsigned char *file1_sha1 = file_sha1(file1);
	unsigned char *file2_sha1 = file_sha1(file2);
	int ret = 0;
	if (memcmp(file1_sha1, file2_sha1, SHA_DIGEST_LENGTH) == 0) {
		ret = 1;
	}
	free(file1_sha1);
	free(file2_sha1);
	return ret;
}

START_TEST (test_encryption_and_decryption_files)
{
	char in_file[] = "check_nachogcm";
	char enc_file[] = "/tmp/check_nachogcm.enc";
	char dec_file[] = "/tmp/check_nachogcm.dec";
	unsigned char key[] = "fdf+PPzb3ZLg800qfIuyZYgJaIJJ03AcyhSVTGIyNOQ=";

	char cwd[1024];
	getcwd(cwd, sizeof(cwd));
	printf("cwd = %s\n", cwd);
	ck_assert(filesize(in_file) > 0);

	int rv = aes_256_gcm_encrypt_file(in_file, enc_file, "Ncho1234567890", 123456, key);
	ck_assert(rv > 0);

	rv = aes_256_gcm_decrypt_file(enc_file, dec_file, key);
	ck_assert(rv > 0);

	ck_assert(compare_files(in_file, dec_file) == 1);
}
END_TEST

Suite * encrypt_suite(void)
{
    Suite *s;
    TCase *tc_encrypt;

    s = suite_create("encrypt_suite");
    tc_encrypt = tcase_create("encrypt_check");
    tcase_add_test(tc_encrypt, test_encryption);
    tcase_add_test(tc_encrypt, test_encryption_and_decryption_lt_1_block);
    tcase_add_test(tc_encrypt, test_encryption_and_decryption_1_block);
    tcase_add_test(tc_encrypt, test_encryption_and_decryption_1_blocks_plus);
    tcase_add_test(tc_encrypt, test_encryption_and_decryption_2_blocks);
    tcase_add_test(tc_encrypt, test_encryption_and_decryption_files);

	suite_add_tcase(s, tc_encrypt);

    return s;
}

