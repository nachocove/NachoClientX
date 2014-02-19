
XBUILD=xcodebuild
PROJECT_ROOT=.
PROJECT=$(PROJECT_ROOT)/NachoPlatform.xcodeproj
TARGET=NachoPlatform

all: libNachoPlatformSDK.a

gen_srcs: src/tld-canon.h

src/tld-canon.h:
	-php ../../registered-domain-libs/generateEffectiveTLDs.php c > src/tld-canon.h

libNachoPlatform-i386.a: gen_srcs
	$(XBUILD) -project $(PROJECT) -target $(TARGET) -sdk iphonesimulator -configuration Release clean build
	-mv $(PROJECT_ROOT)/build/Release-iphonesimulator/lib$(TARGET).a $@

libNachoPlatform-armv7.a: gen_srcs
	$(XBUILD) -project $(PROJECT) -target $(TARGET) -sdk iphoneos -arch armv7 -configuration Release clean build
	-mv $(PROJECT_ROOT)/build/Release-iphoneos/lib$(TARGET).a $@

libNachoPlatformSDK.a: libNachoPlatform-i386.a libNachoPlatform-armv7.a
	lipo -create -output $@ $^

clean:
	-rm -fr build
	-rm -f *.a *.dll
	-rm src/tld-canon.h
	-rm -fr ./NachoPlatform.xcodeproj/project.xcworkspace
	-rm -fr ./NachoPlatform.xcodeproj/xcuserdata
