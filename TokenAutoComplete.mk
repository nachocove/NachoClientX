XAM_ANDROID_HOME = $(shell ls -d1 ${HOME}/Library/Developer/Xamarin/android-sdk*)
include gradle.properties
all:
	@if [ -z "$(XAM_ANDROID_HOME)" ] ; then echo "No XAM_ANDROID_HOME found."; exit 1; fi
	@if [ ! -d "$(XAM_ANDROID_HOME)/build-tools/$(ANDROID_BUILD_TOOLS_VERSION)" ] ; then echo "Need android build-tools $(ANDROID_BUILD_TOOLS_VERSION). Please install it via Android SDK Manager."; exit 1; fi
	@if [ ! -d $(XAM_ANDROID_HOME)/extras/android/m2repository ] ; then echo "Please make sure to install the 'Android Support Repository' in the 'Extras' \n    section of the Android SDK Manager (Xamarin Studio -> Tools -> \n    Open Android SDK Manager...)"; exit 1; fi
	ANDROID_HOME=${XAM_ANDROID_HOME} bash gradlew jar

clean:

