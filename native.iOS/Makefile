PROJECTS := nachosqlite3 nachoregdom nachodns nachodevice nacholog nachoproc nachoreachability

LIBS := $(foreach proj, $(PROJECTS), libs/lib$(proj).a)
PLATFORMS := iphonesimulator/i386 iphonesimulator/x86_64 iphoneos/armv7 iphoneos/arm64

project = $(patsubst lib%.a,%, $(notdir $(1)))
sdk = $(notdir $(call dirnoslash,$(call dirnoslash, $(1))))
arch = $(notdir $(call dirnoslash,$(1)))
built = build/$(patsubst libs/%,%,$(1))
dirnoslash = $(patsubst %/,%,$(dir $(1)))

XBUILD := xcodebuild

.PHONY: all clean
.SECONDARY: $(foreach proj, $(PROJECTS), $(foreach platform, $(PLATFORMS), libs/platforms/$(platform)/lib$(proj).a))

all: $(LIBS)
	@echo "Done"

libs/%.a: $(foreach platform, $(PLATFORMS), libs/platforms/$(platform)/%.a)
	@echo "Creating $@..."
	@mkdir -p $(dir $@)
	@lipo -create -output $@ $^

libs/platforms/%.a: 
	@echo "Building $@..."
	@mkdir -p $(dir $@)
	@$(XBUILD) -project $(call project, $@)/$(call project, $@).xcodeproj -target $(call project, $@) -sdk $(call sdk, $@) -arch $(call arch, $@) -configuration Release build TARGET_BUILD_DIR=../$(dir $(call built, $@)) > /dev/null
	@mv -f $(call built, $@) $@

clean:
	@echo "Cleaning..."
	@rm -rf build
	@rm -rf libs
	@echo "Done"