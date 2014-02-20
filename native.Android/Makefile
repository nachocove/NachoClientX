
all: src_links gen_srcs
	cd jni; ndk-build; cd ..
	-mkdir ../NachoClient.Android/lib
	-mkdir ../NachoClient.Android/lib/armeabi
	-mkdir ../NachoClient.Android/lib/armeabi-v7a
	-mkdir ../NachoClient.Android/lib/x86
	-rm ../NachoClient.Android/lib/armeabi/libnachoplatform.so
	ln libs/armeabi/libnachoplatform.so ../NachoClient.Android/lib/armeabi/libnachoplatform.so
	-rm ../NachoClient.Android/lib/armeabi-v7a/libnachoplatform.so
	ln libs/armeabi-v7a/libnachoplatform.so ../NachoClient.Android/lib/armeabi-v7a/libnachoplatform.so
	-rm ../NachoClient.Android/lib/x86/libnachoplatform.so
	ln libs/x86/libnachoplatform.so ../NachoClient.Android/lib/x86/libnachoplatform.so

src_links: jni/regdom.h jni/regdom.c jni/nacho-dkim-regdom.c jni/nacho-system-properties.c
gen_srcs: jni/tld-canon.h

jni/tld-canon.h:
	-php ../../registered-domain-libs/generateEffectiveTLDs.php c > jni/tld-canon.h
jni/regdom.h:
	ln ../../registered-domain-libs/C/regdom.h jni
jni/regdom.c:
	ln ../../registered-domain-libs/C/regdom.c jni
jni/nacho-dkim-regdom.c:
	ln src/nacho-dkim-regdom.c jni
jni/nacho-system-properties.c:
	ln src/nacho-system-properties.c jni

clean:
	-rm -rf ../NachoClient.Android/lib
	-rm jni/tld-canon.h jni/regdom.h jni/regdom.c jni/nacho-dkim-regdom.c jni/nacho-system-properties.c
	-rm libs/*/*
