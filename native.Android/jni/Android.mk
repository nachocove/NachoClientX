LOCAL_PATH := $(call my-dir)

include $(CLEAR_VARS)

# Here we give our module name and source file(s)
LOCAL_CFLAGS    += -std=c99
LOCAL_MODULE    := nachoplatform
LOCAL_SRC_FILES := regdom.c nacho-dkim-regdom.c nacho-system-properties.c nc_sqlite3.c

include $(BUILD_SHARED_LIBRARY)
