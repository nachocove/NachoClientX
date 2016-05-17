
XBUILD=xcodebuild
PROJECT_ROOT=.
PROJECT=$(PROJECT_ROOT)/NachoPlatform.xcodeproj
TARGET=NachoPlatform

all: libNachoPlatformSDK.a libcrypto.a

gen_srcs: src/tld-canon.h

src/tld-canon.h:
	-php ../../registered-domain-libs/generateEffectiveTLDs.php c > src/tld-canon.h

libNachoPlatform-x86_64.a: gen_srcs
	$(XBUILD) -project $(PROJECT) -target $(TARGET) -sdk macosx -arch x86_64 -configuration Release clean build TARGET_BUILD_DIR='./build/build-x86_64'
	mv -f ./build/build-x86_64/lib$(TARGET).a $@

libNachoPlatformSDK.a: libNachoPlatform-x86_64.a
	lipo -create -output $@ $^

libcrypto.a: 
	cp ../../ios-openssl/lib/libcrypto.a .

clean:
	rm -fr build
	rm -f *.a *.dll
	rm -f src/tld-canon.h
	rm -fr ./NachoPlatform.xcodeproj/project.xcworkspace
	rm -fr ./NachoPlatform.xcodeproj/xcuserdata
