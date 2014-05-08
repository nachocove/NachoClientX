# This is a Makefile that converts ActiveSync .xsd files into our own XML
# config format. However, the .xsd files are incomplete and have errors.
# So, this separate Makefile is created to automate the bootstraping of
# XML config files. In the future, we'll directly modify the XML config
# files.

AS_XSD := python as_xsd.py
AS_XSD_FILES := as_xsd.py as_xml.py object_stack.py output.py

request_xsd = $(addprefix xsd/Request/,$(addsuffix .xsd,$(1)))
response_xsd = $(addprefix xsd/Response/,$(addsuffix .xsd,$(1)))

AIRSYNC_CMD := Sync
AIRSYNC_REQUEST_FILES := $(call request_xsd,$(AIRSYNC_CMD))
AIRSYNC_RESPONSE_FILES := $(call response_xsd,$(AIRSYNC_CMD))

COMPOSEMAIL_CMD := SendMail SmartForward SmartReply
COMPOSEMAIL_REQUEST_FILES := $(call request_xsd,$(COMPOSEMAIL_CMD))
COMPOSEMAIL_RESPONSE_FILES := $(call response_xsd,$(COMPOSEMAIL_CMD))

FOLDERHIERARCHY_CMD := FolderCreate FolderUpdate FolderDelete FolderSync
FOLDERHIERARCHY_REQUEST_FILES := $(call request_xsd,$(FOLDERHIERARCHY_CMD))
FOLDERHIERARCHY_RESPONSE_FILES := $(call response_xsd,$(FOLDERHIERARCHY_CMD))

GETITEMESTIMATE_CMD := GetItemEstimate
GETITEMESTIMATE_REQUEST_FILES := $(call request_xsd,$(GETITEMESTIMATE_CMD))
GETITEMESTIMATE_RESPONSE_FILES := $(call response_xsd,$(GETITEMESTIMATE_CMD))

ITEMOPERATIONS_CMD := ItemOperations
ITEMOPERATIONS_REQUEST_FILES := $(call request_xsd,$(ITEMOPERATIONS_CMD))
ITEMOPERATIONS_RESPONSE_FILES := $(call response_xsd,$(ITEMOPERATIONS_CMD))

MEETINGRESPONSE_CMD := MeetingResponse
MEETINGRESPONSE_REQUEST_FILES := $(call request_xsd,$(MEETINGRESPONSE_CMD))
MEETINGRESPONSE_RESPONSE_FILES := $(call response_xsd,$(MEETINGRESPONSE_CMD))

MOVE_CMD := MoveItems
MOVE_REQUEST_FILES := $(call request_xsd,$(MOVE_CMD))
MOVE_RESPONSE_FILES := $(call response_xsd,$(MOVE_CMD))

PING_CMD := Ping
PING_REQUEST_FILES := $(call request_xsd,$(PING_CMD))
PING_RESPONSE_FILES := $(call response_xsd,$(PING_CMD))

PROVISION_CMD := Provision
PROVISION_REQUEST_FILES := $(call request_xsd,$(PROVISION_CMD))
PROVISION_RESPONSE_FILES := $(call response_xsd,$(PROVISION_CMD))

RESOLVERECIPIENTS_CMD := ResolveRecipients
RESOLVERECIPIENTS_REQUEST_FILES := $(call request_xsd,$(RESOLVERECIPIENTS_CMD))
RESOLVERECIPIENTS_RESPONSE_FILES := $(call response_xsd,$(RESOLVERECIPIENTS_CMD))

SEARCH_CMD := Search
SEARCH_REQUEST_FILES := $(call request_xsd,$(SEARCH_CMD))
SEARCH_RESPONSE_FILES := $(call response_xsd,$(SEARCH_CMD))

SETTINGS_CMD := Settings
SETTINGS_REQUEST_FILES := $(call request_xsd,$(SETTINGS_CMD))
SETTINGS_RESPONSE_FILES := $(call response_xsd,$(SETTINGS_CMD))

include namespaces.mk

COMMON_XML_FILES := $(addprefix xsd/common/,$(addsuffix .xml,$(COMMON_NAMESPACES)))

XML_FILES := \
	$(addprefix xsd/Request/,$(addsuffix .xml,$(REQUEST_NAMESPACES))) \
	$(addprefix xsd/Response/,$(addsuffix .xml,$(RESPONSE_NAMESPACES))) \
	$(addprefix xsd/common/,$(addsuffix .xml,$(COMMON_NAMESPACES))) \

all: $(XML_FILES)

xsd/Request/AirSync.xml : $(AIRSYNC_REQUEST_FILES) xsd/common/AirSync.xsd
	$(AS_XSD) --out-file $@ $(AIRSYNC_REQUEST_FILES)

xsd/Response/AirSync.xml : $(AIRSYNC_RESPONSE_FILES) xsd/common/AirSync.xsd
	$(AS_XSD) --out-file $@ $(AIRSYNC_RESPONSE_FILES)

xsd/Request/ComposeMail.xml : $(COMPOSEMAIL_REQUEST_FILES) xsd/common/ComposeMail.xsd
	$(AS_XSD) --out-file $@ $(COMPOSEMAIL_REQUEST_FILES)

xsd/Response/ComposeMail.xml : $(COMPOSEMAIL_RESPONSE_FILES) xsd/common/ComposeMail.xsd
	$(AS_XSD) --out-file $@ $(COMPOSEMAIL_RESPONSE_FILES)

xsd/Request/FolderHierarchy.xml : $(FOLDERHIERARCHY_REQUEST_FILES) xsd/common/FolderHierarchy.xsd
	$(AS_XSD) --out-file $@ $(FOLDERHIERARCHY_REQUEST_FILES)

xsd/Response/FolderHierarchy.xml : $(FOLDERHIERARCHY_RESPONSE_FILES) xsd/common/FolderHierarchy.xsd
	$(AS_XSD) --out-file $@ $(FOLDERHIERARCHY_RESPONSE_FILES)

# GetItemEstimate is handled by the default rule

xsd/Request/ItemOperations.xml : $(ITEMOPERATIONS_REQUEST_FILES) xsd/common/ItemOperations.xsd
	$(AS_XSD) --out-file $@ $(ITEMOPERATIONS_REQUEST_FILES)

xsd/Response/ItemOperations.xml : $(ITEMOPERATIONS_RESPONSE_FILES) xsd/common/ItemOperations.xsd
	$(AS_XSD) --out-file $@ $(ITEMOPERATIONS_RESPONSE_FILES)

# MeetingResponse is handled by the default rule

xsd/Request/Move.xml : $(MOVE_REQUEST_FILES) 
	$(AS_XSD) --out-file $@ $(MOVE_REQUEST_FILES)

xsd/Response/Move.xml : $(MOVE_RESPONSE_FILES) 
	$(AS_XSD) --out-file $@ $(MOVE_RESPONSE_FILES)

# Ping is handled by the default rule

xsd/Request/Provision.xml : $(PROVISION_REQUEST_FILES) xsd/common/Provision.xsd
	$(AS_XSD) --out-file $@ $(PROVISION_REQUEST_FILES)

xsd/Response/Provision.xml : $(PROVISION_RESPONSE_FILES) xsd/common/Provision.xsd
	$(AS_XSD) --out-file $@ $(PROVISION_RESPONSE_FILES)

# ResolveRecipients is handled by the default rule

xsd/Request/Search.xml : $(SEARCH_REQUEST_FILES) xsd/common/Search.xsd
	$(AS_XSD) --out-file $@ $(SEARCH_REQUEST_FILES)

xsd/Response/Search.xml : $(SEARCH_RESPONSE_FILES) xsd/common/Search.xsd
	$(AS_XSD) --out-file $@ $(SEARCH_RESPONSE_FILES)

xsd/Request/Settings.xml : $(SETTINGS_REQUEST_FILES) xsd/common/Settings.xsd
	$(AS_XSD) --out-file $@ $(SETTINGS_REQUEST_FILES)

xsd/Response/Settings.xml : $(SETTINGS_RESPONSE_FILES) xsd/common/Settings.xsd
	$(AS_XSD) --out-file $@ $(SETTINGS_RESPONSE_FILES)

# Default rule for XML file that does not depend on common .xsd files.
# And for request / response, the command has the same name as the namespace.
%.xml : %.xsd 
	$(AS_XSD) --out-file $@ $<

clean:
	rm -f $(XML_FILES)