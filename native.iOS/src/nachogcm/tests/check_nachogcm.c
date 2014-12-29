/*
 * check_nachogcm.c
 *
 *  Created on: Dec 26, 2014
 *      Author: jan_vilhuber
 */
#include <stdlib.h>
#include <check.h>
#include <getopt.h>
#include <stdio.h>

#include "test_encrypt.h"
#include "test_keygeneration.h"
#include "test_iv.h"

int main(int argc, char **argv) {
    int number_failed;
    SRunner *sr;
    enum fork_status fstat = CK_FORK;
    enum print_output print_mode = CK_MINIMAL;

	static struct option long_options[] = {
			{"nofork",    no_argument,       0,  'n' },
			{"verbose",    no_argument,       0,  'v' },
			{"silent",    no_argument,       0,  's' },
			{"debug",    no_argument,       0,  'd' },
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
		case '?':
			break;

		case 'n':
			fstat = CK_NOFORK;
			break;

		case 'v':
			print_mode = CK_NORMAL;
			break;

		case 'd':
			print_mode = CK_VERBOSE;
			break;

		case 's':
			print_mode = CK_SILENT;
			break;

		case 'h':
			printf("USAGE: %s [-h] [-n]\n", argv[0]);
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

	sr = srunner_create(encrypt_suite());
	srunner_set_fork_status(sr, fstat);
	srunner_add_suite(sr, key_generation_suite());
	srunner_add_suite(sr, iv_generation_suite());

	srunner_run_all(sr, print_mode);
    number_failed = srunner_ntests_failed(sr);
    srunner_free(sr);
    return (number_failed == 0) ? EXIT_SUCCESS : EXIT_FAILURE;
}
