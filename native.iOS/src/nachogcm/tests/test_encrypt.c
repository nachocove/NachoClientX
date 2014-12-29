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

START_TEST (test_encryption_and_decryption)
{
	char in_data[] = "hello world";
	unsigned int in_len = strlen(in_data);
	unsigned char *out_memory = NULL;
	unsigned int out_len = 0;
	unsigned char key[] = "fdf+PPzb3ZLg800qfIuyZYgJaIJJ03AcyhSVTGIyNOQ=";
	unsigned char *in_memory = NULL;


	int rv = aes_256_gcm_encrypt_memory((unsigned char *)in_data, in_len, &out_memory, &out_len, "Ncho12345", 12345, key);
	ck_assert_int_gt(rv, 0);
	ck_assert_ptr_ne(out_memory, 0x0);

	in_memory = out_memory;
	in_len = out_len;

	out_memory = NULL;
	out_len = 0;
	rv = aes_256_gcm_decrypt_memory(in_memory, in_len, &out_memory, &out_len, key);
	ck_assert_int_gt(rv, 0);
	ck_assert_ptr_ne(out_memory, 0x0);
	free(in_memory);
	free(out_memory);
}
END_TEST


Suite * encrypt_suite(void)
{
    Suite *s;
    TCase *tc_encrypt;

    s = suite_create("encrypt_suite");
    tc_encrypt = tcase_create("encrypt_check");
    tcase_add_test(tc_encrypt, test_encryption);
    tcase_add_test(tc_encrypt, test_encryption_and_decryption);


	suite_add_tcase(s, tc_encrypt);

    return s;
}

