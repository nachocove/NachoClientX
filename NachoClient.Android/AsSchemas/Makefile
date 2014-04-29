AS_SCHEMA_FILES := as_schema.py object_stack.py
AS_SCHEMA := python as_schema.py

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

AS_XML_FILTER_REQUEST_FILES := $(patsubst %.xsd,%Request.cs,$(notdir $(XSD_REQUEST_FILES)))
AS_XML_FILTER_RESPONSE_FILES := $(patsubst %.xsd,%Response.cs,$(notdir $(XSD_RESPONSE_FILES)))
AS_XML_FILTER_COMMON_FILES := $(patsubst %.xsd,%.cs,$(notdir $(XSD_COMMON_FILES)))

AS_XML_FILTER_FILES := $(addprefix AsXmlFilter,$(AS_XML_FILTER_REQUEST_FILES) $(AS_XML_FILTER_RESPONSE_FILES) $(AS_XML_FILTER_COMMON_FILES))

all: $(AS_XML_FILTER_FILES)

$(AS_XML_FILTER_FILES) : $(XML_SCHEMA_FILES)

%.xml : %.xsd as_xsd.py object_stack.py
	python as_xsd.py $<

AsXmlFilterPingRequest.cs : xsd/Request/Ping.xml $(AS_SCHEMA_FILES)
	$(AS_SCHEMA) $< PingRequest

AsXmlFilterPingResponse.cs : xsd/Response/Ping.xml $(AS_SCHEMA_FILES)
	$(AS_SCHEMA) $< PingResponse

AsXmlFilter%.cs : xsd/common/%.xml $(AS_SCHEMA_FILES)
	$(AS_SCHEMA) $< $(basename $@)

clean:
	rm -f $(AS_XML_FILTER_FILES)
	rm -f $(XML_SCHEMA_FILES)
