/*
 * test_iv.c
 *
 *  Created on: Dec 26, 2014
 *      Author: jan_vilhuber
 */

#include <check.h>
#include <stdio.h>
#include <sys/signal.h>
#include <stdlib.h>

#include "nachogcm/iv.h"

START_TEST(test_create_iv)
{
	unsigned char *iv = create_iv("Ncho12345", 1234, 512);
	ck_assert_ptr_ne(iv, 0x0);
}
END_TEST

START_TEST(test_create_iv_bad_len)
{
	unsigned char *iv = create_iv("Ncho12345", 1234, 513);
	ck_assert_ptr_eq(iv, 0x0);
}
END_TEST

START_TEST(test_create_iv_memory)
{
	int iv_len = (512/8)+1;
	unsigned char *iv = malloc(iv_len);
	memset(iv, 0x0, iv_len);
	int rv = create_iv_into_buffer("Ncho12345", 12345, iv, iv_len);
	ck_assert_int_eq(rv, iv_len);
	ck_assert_ptr_ne(iv, 0x0);
	ck_assert(iv[iv_len] == 0x0);
}
END_TEST

Suite * iv_generation_suite(void)
{
    Suite *s;
    TCase *tc;

    s = suite_create("iv_generation");
    tc = tcase_create("check_iv");
    tcase_add_test(tc, test_create_iv);
    tcase_add_test(tc, test_create_iv_bad_len);
    tcase_add_test(tc, test_create_iv_memory);

    suite_add_tcase(s, tc);

    return s;
}

