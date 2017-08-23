//
//  PlatformProcess.h
//  NachoPlatformLib
//
//  Created by Henry Kwok on 10/9/14.
//  Copyright (c) 2014 Nacho Cove, Inc. All rights reserved.
//

#ifndef nachoproc_proc_h
#define nachoproc_proc_h

long long nacho_get_used_memory();
int nacho_get_current_number_of_file_descriptors();
int nacho_get_current_number_of_in_use_file_descriptors();
int nacho_get_current_in_use_file_descriptors(int* fds, int limit);
void nacho_get_filename_for_descriptor(int fd, char* name, int limit);
int nacho_get_number_of_system_threads();
char** nacho_get_stack_trace();

#endif
