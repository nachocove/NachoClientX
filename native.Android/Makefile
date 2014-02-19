
all: src_links gen_srcs
	cd Android/jni; ndk-build

src_links: jni/regdom.h jni/regdom.c jni/nacho-dkim-regdom.c jni/nacho-system-properties.c
gen_srcs: jni/tld-canon.h

jni/tld-canon.h:
	-php ../../registered-domain-libs/generateEffectiveTLDs.php c > jni/tld-canon.h
jni/regdom.h:
	ln ../../registered-domain-libs/C/regdom.h jni
jni/regdom.c:
	ln ../../registered-domain-libs/C/regdom.c jni
jni/nacho-dkim-regdom.c:
	ln ../native.common/src/nacho-dkim-regdom.c jni
jni/nacho-system-properties.c:
	ln src/nacho-system-properties.c jni

clean:
	-rm jni/tld-canon.h jni/regdom.h jni/regdom.c jni/nacho-dkim-regdom.c jni/nacho-system-properties.c
	-rm libs/*/*
