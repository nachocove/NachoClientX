
all: src_links gen_srcs
	@if [ ${NDK}"x" == "x" ]; then echo Nacho native.Android/Makefile needs NDK to be set ; exit 1; fi
	cd jni; ${NDK}/ndk-build; cd ..
	mkdir -p ../NachoClient.Android/lib/armeabi
	mkdir -p ../Test.Android/lib/armeabi
	mkdir -p ../NachoClient.Android/lib/armeabi-v7a
	mkdir -p ../NachoClient.Android/lib/arm64-v8a
	mkdir -p ../Test.Android/lib/armeabi-v7a
	mkdir -p ../Test.Android/lib/arm64-v8a
	mkdir -p ../NachoClient.Android/lib/x86
	mkdir -p ../Test.Android/lib/x86
	rm -f ../NachoClient.Android/lib/armeabi/libnachoplatform.so
	rm -f ../Test.Android/lib/armeabi/libnachoplatform.so
	ln libs/armeabi/libnachoplatform.so ../NachoClient.Android/lib/armeabi/libnachoplatform.so
	ln libs/armeabi/libnachoplatform.so ../Test.Android/lib/armeabi/libnachoplatform.so
	rm -f ../NachoClient.Android/lib/armeabi-v7a/libnachoplatform.so
	rm -f ../Test.Android/lib/armeabi-v7a/libnachoplatform.so
	ln libs/armeabi-v7a/libnachoplatform.so ../NachoClient.Android/lib/armeabi-v7a/libnachoplatform.so
	ln libs/armeabi-v7a/libnachoplatform.so ../Test.Android/lib/armeabi-v7a/libnachoplatform.so
	rm -f ../NachoClient.Android/lib/x86/libnachoplatform.so
	rm -f ../Test.Android/lib/x86/libnachoplatform.so
	ln libs/x86/libnachoplatform.so ../NachoClient.Android/lib/x86/libnachoplatform.so
	ln libs/x86/libnachoplatform.so ../Test.Android/lib/x86/libnachoplatform.so
	rm -f ../NachoClient.Android/lib/arm64-v8a/libnachoplatform.so
	rm -f ../Test.Android/lib/arm64-v8a/libnachoplatform.so
	ln libs/arm64-v8a/libnachoplatform.so ../NachoClient.Android/lib/arm64-v8a/libnachoplatform.so
	ln libs/arm64-v8a/libnachoplatform.so ../Test.Android/lib/arm64-v8a/libnachoplatform.so

src_links: jni/regdom.h jni/regdom.c jni/nacho-dkim-regdom.c jni/nacho-system-properties.c jni/nc_sqlite3.c
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
jni/nc_sqlite3.c:
	ln ../../NachoPlatformBinding/NachoPlatformLib/NachoPlatformLib/nc_sqlite3.c jni

clean:
	rm -rf ../NachoClient.Android/lib
	rm -f jni/tld-canon.h jni/regdom.h jni/regdom.c jni/nacho-dkim-regdom.c jni/nacho-system-properties.c
	rm -fr libs/ obj/
