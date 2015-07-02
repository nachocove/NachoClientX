/*
 * check_nachogcm.c
 *
 *  Created on: Jul 2, 2015
 *      Author: jan_vilhuber
 */

#include <stdlib.h>
#include <check.h>

#include "test_encrypt.h"
#include "test_iv.h"
#include "test_keygeneration.h"

int main(void)
{
	int number_failed;
	Suite *s;
	SRunner *sr;

	s = encrypt_suite();
	sr = srunner_create(s);

	s = iv_generation_suite();
	srunner_add_suite(sr, s);

	s = key_generation_suite();
	srunner_add_suite(sr, s);

	srunner_run_all(sr, CK_MINIMAL);
	number_failed = srunner_ntests_failed(sr);
	srunner_free(sr);
	return (number_failed == 0) ? EXIT_SUCCESS : EXIT_FAILURE;
}
