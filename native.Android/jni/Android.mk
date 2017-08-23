LOCAL_PATH := $(call my-dir)

include $(CLEAR_VARS)
LOCAL_CFLAGS    += -std=c99
LOCAL_MODULE    := nachosqlite3
LOCAL_SRC_FILES := nc_sqlite3.c
include $(BUILD_SHARED_LIBRARY)

include $(CLEAR_VARS)
LOCAL_CFLAGS    += -std=c99
LOCAL_MODULE    := nachoregdom
LOCAL_SRC_FILES := regdom.c nacho-dkim-regdom.c
include $(BUILD_SHARED_LIBRARY)
