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

PING_CMD := Ping
PING_REQUEST_FILES := $(call request_xsd,$(PING_CMD))
PING_RESPONSE_FILES := $(call response_xsd,$(PING_CMD))

PROVISION_CMD := Provision
PROVISION_REQUEST_FILES := $(call request_xsd,$(PROVISION_CMD))
PROVISION_RESPONSE_FILES := $(call response_xsd,$(PROVISION_CMD))

SETTINGS_CMD := Settings
SETTINGS_REQUEST_FILES := $(call request_xsd,$(SETTINGS_CMD))
SETTINGS_RESPONSE_FILES := $(call response_xsd,$(SETTINGS_CMD))

REQUEST_NAMESPACES := \
	AirSync \
	ComposeMail \
	FolderHierarchy \
	Ping \
	Provision \
	Settings \

RESPONSE_NAMESPACES := $(REQUEST_NAMESPACES)

COMMON_NAMESPACES := \
	AirSyncBase \
	Calendar \
	Contacts \
	Contacts2 \
	DocumentLibrary \
	Email \
	Email2 \
	ItemOperations \
	RightsManagement \
	Tasks \

# These common namespace do not produce its own XML config files because
# they are included into requests / responses
#
# Provision

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

xsd/Request/Provision.xml : $(PROVISION_REQUEST_FILES) xsd/common/Provision.xsd
	$(AS_XSD) --out-file $@ $(PROVISION_REQUEST_FILES)

xsd/Response/Provision.xml : $(PROVISION_RESPONSE_FILES) xsd/common/Provision.xsd
	$(AS_XSD) --out-file $@ $(PROVISION_RESPONSE_FILES)

xsd/Request/Settings.xml : $(SETTINGS_REQUEST_FILES) xsd/common/Settings.xsd
	$(AS_XSD) --out-file $@ $(SETTINGS_REQUEST_FILES)

xsd/Response/Settings.xml : $(SETTINGS_RESPONSE_FILES) xsd/common/Settings.xsd
	$(AS_XSD) --out-file $@ $(SETTINGS_RESPONSE_FILES)

%.xml : %.xsd 
	$(AS_XSD) --out-file $@ $<

clean:
	rm -f $(XML_FILES)