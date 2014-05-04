# This is a Makefile that converts ActiveSync .xsd files into our own XML
# config format. However, the .xsd files are incomplete and have errors.
# So, this separate Makefile is created to automate the bootstraping of
# XML config files. In the future, we'll directly modify the XML config
# files.

AS_XSD_FILES := as_xsd.py as_xml.py object_stack.py output.py

XSD_REQUEST_FILES := \
	xsd/Request/Ping.xsd \

XSD_RESPONSE_FILES := \
	xsd/Response/Ping.xsd \

XSD_COMMON_FILES := \
	xsd/common/Provision.xsd \
	xsd/common/AirSyncBase.xsd \
	xsd/common/Calendar.xsd \
	xsd/common/Contacts.xsd \
	xsd/common/Contacts2.xsd \
	xsd/common/DocumentLibrary.xsd \
	xsd/common/Email.xsd \
	xsd/common/Email2.xsd \
	xsd/common/ItemOperations.xsd \
	xsd/common/RightsManagement.xsd \
	xsd/common/Tasks.xsd

XSD_FILES := $(XSD_REQUEST_FILES) $(XSD_RESPONSE_FILES) $(XSD_COMMON_FILES)

XML_SCHEMA_FILES := $(patsubst %.xsd,%.xml,$(XSD_FILES))

all: $(XML_SCHEMA_FILES)

%.xml : %.xsd $(AS_XSD_FILES)
	python as_xsd.py $<

clean:
	rm -f $(XML_SCHEMA_FILES)