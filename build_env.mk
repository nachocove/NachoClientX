# All build environment related variables go here.

# Tools
XAMARIN_STUDIO_DIR := /Applications/Xamarin\ Studio.app/Contents/MacOS
MDTOOL := $(XAMARIN_STUDIO_DIR)/mdtool

# Build configuration - Debug / Release
ifndef CONFIG
# This variable is used for clean target. If there is no CONFIG
# defined on command line, clean will clean all targets
HAS_CLI_CONFIG := No
endif
$(info Build configuration is $(CONFIG))

