
XBUILD=xcodebuild
PROJECT_ROOT=.
PROJECT=$(PROJECT_ROOT)/NachoPlatform.xcodeproj
TARGET=NachoPlatform

all: libNachoPlatformSDK.a libcrypto.a libConcierge.a libFirebase.a libWebRTC.a

gen_srcs: src/tld-canon.h

src/tld-canon.h:
	-php ../../registered-domain-libs/generateEffectiveTLDs.php c > src/tld-canon.h

libNachoPlatform-i386.a: gen_srcs
	$(XBUILD) -project $(PROJECT) -target $(TARGET) -sdk iphonesimulator -configuration Release clean build
	mv -f $(PROJECT_ROOT)/build/Release-iphonesimulator/lib$(TARGET).a $@

libNachoPlatform-x86_64.a: gen_srcs
	$(XBUILD) -project $(PROJECT) -target $(TARGET) -sdk iphonesimulator -arch x86_64 -configuration Release clean build TARGET_BUILD_DIR='./build/build-x86_64'
	mv -f ./build/build-x86_64/lib$(TARGET).a $@

libNachoPlatform-armv7.a: gen_srcs
	$(XBUILD) -project $(PROJECT) -target $(TARGET) -sdk iphoneos -arch armv7 -configuration Release clean build
	mv -f $(PROJECT_ROOT)/build/Release-iphoneos/lib$(TARGET).a $@

libNachoPlatform-arm64.a: gen_srcs
	$(XBUILD) -project $(PROJECT) -target $(TARGET) -sdk iphoneos -arch arm64 -configuration Release clean build TARGET_BUILD_DIR='./build/build-arm64'
	mv -f ./build/build-arm64/lib$(TARGET).a $@

libNachoPlatformSDK.a: libNachoPlatform-i386.a libNachoPlatform-armv7.a libNachoPlatform-arm64.a
	lipo -create -output $@ $^

libcrypto.a: 
	cp ../../ios-openssl/lib/libcrypto.a .

libConcierge.a:
	cp ../../Citrix-Concierge/Concierge/Concierge.iOS/SDK/Concierge.framework/Concierge libConcierge.a

libFirebase.a:
	cp ../../Citrix-Concierge/Concierge/Concierge.iOS/SDK/Firebase.framework/Versions/A/Firebase libFirebase.a

libWebRTC.a:
	cp ../../Citrix-Concierge/Concierge/Concierge.iOS/SDK/WebRTC.framework/Versions/A/WebRTC libWebRTC.a

clean:
	rm -fr build
	rm -f *.a *.dll
	rm -f src/tld-canon.h
	rm -fr ./NachoPlatform.xcodeproj/project.xcworkspace
	rm -fr ./NachoPlatform.xcodeproj/xcuserdata
