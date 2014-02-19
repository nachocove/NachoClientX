LOCAL_PATH := $(call my-dir)

include $(CLEAR_VARS)

# Here we give our module name and source file(s)
LOCAL_MODULE    := nachoplatform
LOCAL_SRC_FILES := dkim-regdom.c nacho-dkim-regdom.c nacho-system-properties.c

include $(BUILD_SHARED_LIBRARY)
