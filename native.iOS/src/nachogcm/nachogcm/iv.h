/*
 * gcm_iv.h
 *
 *  Created on: Dec 23, 2014
 *      Author: jan_vilhuber
 */

#ifndef GCM_IV_H_
#define GCM_IV_H_

unsigned char *create_iv(const char *device_id, long counter, int iv_len_bits);

int create_iv_into_buffer(const char *device_id, long counter, unsigned char *buffer, int iv_len);

#endif /* GCM_IV_H_ */
