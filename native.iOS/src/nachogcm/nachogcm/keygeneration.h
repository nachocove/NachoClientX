/*
 * keygeneration.h
 *
 *  Created on: Dec 26, 2014
 *      Author: jan_vilhuber
 */

#ifndef NACHOGCM_KEYGENERATION_H_
#define NACHOGCM_KEYGENERATION_H_

unsigned char *make_key(int bits);
unsigned int make_key_into_buffer(int bits, unsigned char *buffer);

#endif /* NACHOGCM_KEYGENERATION_H_ */
