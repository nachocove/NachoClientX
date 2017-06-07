
XBUILD=xcodebuild
PROJECT_ROOT=.
PROJECT=$(PROJECT_ROOT)/NachoPlatform.xcodeproj
TARGET=NachoPlatform

all: libNachoPlatformSDK.a libcrypto.a

gen_srcs: src/tld-canon.h

src/tld-canon.h:
	-php ../../registered-domain-libs/generateEffectiveTLDs.php c > src/tld-canon.h

libNachoPlatform-i386.a: gen_srcs
	$(XBUILD) -project $(PROJECT) -target $(TARGET) -sdk iphonesimulator -configuration Release build
	mv -f $(PROJECT_ROOT)/build/Release-iphonesimulator/lib$(TARGET).a $@

libNachoPlatform-x86_64.a: gen_srcs
	$(XBUILD) -project $(PROJECT) -target $(TARGET) -sdk iphonesimulator -arch x86_64 -configuration Release build TARGET_BUILD_DIR='./build/build-x86_64'
	mv -f ./build/build-x86_64/lib$(TARGET).a $@

libNachoPlatform-armv7.a: gen_srcs
	$(XBUILD) -project $(PROJECT) -target $(TARGET) -sdk iphoneos -arch armv7 -configuration Release build
	mv -f $(PROJECT_ROOT)/build/Release-iphoneos/lib$(TARGET).a $@

libNachoPlatform-arm64.a: gen_srcs
	$(XBUILD) -project $(PROJECT) -target $(TARGET) -sdk iphoneos -arch arm64 -configuration Release build TARGET_BUILD_DIR='./build/build-arm64'
	mv -f ./build/build-arm64/lib$(TARGET).a $@

libNachoPlatformSDK.a: libNachoPlatform-i386.a libNachoPlatform-armv7.a libNachoPlatform-arm64.a
	lipo -create -output $@ $^

# The way our build system works, ios-openssl should have already been built by
# NachoPlatformBinding.  Including a make call here because it'll ensure the file
# exists and won't cost any time if openssl is already built
libcrypto.a: 
	make -C ../../ios-openssl
	cp ../../ios-openssl/lib/libcrypto.a .

clean:
	$(XBUILD) -project $(PROJECT) -target $(TARGET) -sdk iphonesimulator -configuration Release clean
	$(XBUILD) -project $(PROJECT) -target $(TARGET) -sdk iphonesimulator -arch x86_64 -configuration Release clean TARGET_BUILD_DIR='./build/build-x86_64'
	$(XBUILD) -project $(PROJECT) -target $(TARGET) -sdk iphoneos -arch armv7 -configuration Release clean
	$(XBUILD) -project $(PROJECT) -target $(TARGET) -sdk iphoneos -arch arm64 -configuration Release clean TARGET_BUILD_DIR='./build/build-arm64'
	rm -f *.a *.dll
	rm -f src/tld-canon.h
# intentionally not calling clean on ios-openssl because NachoPlatformBinding is really in charge of it
