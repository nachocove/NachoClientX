# This list of ABIS should match what is specified in jni/Application.mk
ABIS := armeabi armeabi-v7a arm64-v8a x86

# This list of LIBS should match what is specified in jni/Android.mk
LIBS := nachosqlite3 nachoregdom

# These source files should match what is required by jni/Android.mk (including referenced headers)
# We have single copies of these files in common areas that get linked to jni just for the build
NDKSOURCES := \
	jni/tld-canon.h \
	jni/regdom.h \
	jni/regdom.c \
	jni/nacho-dkim-regdom.c \
	jni/nc_sqlite3.c

jni/tld-canon.h:
	@echo "Fetching TLD canon..."
	@-php ../../registered-domain-libs/generateEffectiveTLDs.php c > jni/tld-canon.h

jni/regdom.h:
	@ln ../../registered-domain-libs/C/regdom.h jni

jni/regdom.c:
	@ln ../../registered-domain-libs/C/regdom.c jni

jni/nc_sqlite3.c:
	@ln ../SQLite/nc_sqlite3.c jni

jni/nacho-dkim-regdom.c:
	@ln src/nacho-dkim-regdom.c jni


# The basic strategey of this build is to do an ndk-build, which create a .so file for each combination of LIBS and ABIS
# The .so files are found in lib/<ABI>/lib<LIB>.so, and need to be copied to NachoClient.Android/lib and Test.Android/lib
NDKLIBROOT := libs
NDKLIBS := $(foreach abi, $(ABIS), $(foreach lib, $(LIBS), $(NDKLIBROOT)/$(abi)/lib$(lib).so))

CLIENTLIBROOT := ../NachoClient.Android/lib
CLIENTLIBS := $(foreach abi, $(ABIS), $(foreach lib, $(LIBS), $(CLIENTLIBROOT)/$(abi)/lib$(lib).so))

TESTLIBROOT := ../Test.Android/lib
TESTLIBS := $(foreach abi, $(ABIS), $(foreach lib, $(LIBS), $(TESTLIBROOT)/$(abi)/lib$(lib).so))

.PHONY: all clean ndkenv ndkbuild ndkclean
.DEFAULT_GOAL := all

all: $(CLIENTLIBS) $(TESTLIBS)
	@echo "Done"

$(CLIENTLIBROOT)/%.so: $(NDKLIBROOT)/%.so
	@echo "Installing " $@
	@rm -f $@
	@mkdir -p $(dir $@)
	@ln $< $@

$(TESTLIBROOT)/%.so: $(NDKLIBROOT)/%.so
	@echo "Installing " $@
	@rm -f $@
	@mkdir -p $(dir $@)
	@ln $< $@

$(NDKLIBS): ndkbuild

ndkenv:
	@if [ ${NDK}"x" == "x" ]; then echo Nacho native.Android/Makefile needs NDK to be set ; exit 1; fi

ndkbuild: ndkenv $(NDKSOURCES)
	@echo "Building NDK libraries..."
	@cd jni
	@${NDK}/ndk-build > /dev/null
	@cd ..

ndkclean: ndkenv
	@echo "Cleaning..."
	@cd jni
	@${NDK}/ndk-build clean > /dev/null
	@cd ..

clean: ndkclean
	@rm -rf $(CLIENTLIBROOT)
	@rm -rf $(TESTLIBROOT)
	@rm -f $(NDKSOURCES)
	@echo "Done"
