XAM_ANDROID_HOME = $(shell ls -d1 ${HOME}/Library/Developer/Xamarin/android-sdk*)
all:
	if [ -z "$(XAM_ANDROID_HOME)" ] ; then echo "No XAM_ANDROID_HOME found."; exit 1; fi
	ANDROID_HOME=${XAM_ANDROID_HOME} bash gradlew jar

clean:

