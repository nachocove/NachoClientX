/*
 * test_keygeneration.c
 *
 *  Created on: Dec 26, 2014
 *      Author: jan_vilhuber
 */

#include <check.h>
#include <stdlib.h>
#include <string.h>

#include "../nachogcm/keygeneration.h"

START_TEST (test_key_generation_into_buffer)
{
	// make sure we don't overfill the buffer
	// heuristic: Make sure not all fields are 0 (that would be a bad key anyway)
	// and that the extra byte at the end is NULL (meaning we didn't overwrite it).
	int bits = 256;
	int buffer_len = bits/8;
	int length_created = 0;
	unsigned char *buffer = malloc(buffer_len+1);
	memset(buffer, 0x0, buffer_len+1);
	length_created = make_key_into_buffer(bits, buffer);
	ck_assert_int_eq(length_created, buffer_len);
	ck_assert(buffer[buffer_len] == 0x0);
	unsigned long sum = 0;
	for (int i=0; i< buffer_len-1; i++) {
		sum += (unsigned long)buffer[i];
	}
	ck_assert_msg(sum != 0, "All elements in key are 0!");
}
END_TEST

START_TEST (test_key_generation)
{
	unsigned char *key = make_key(256);
	ck_assert(key != NULL);
}
END_TEST

Suite * key_generation_suite(void)
{
    Suite *s;
    TCase *tc;

    s = suite_create("key_generation");
    tc = tcase_create("check_key");
    tcase_add_test(tc, test_key_generation_into_buffer);
    tcase_add_test(tc, test_key_generation);
    suite_add_tcase(s, tc);

    return s;
}

