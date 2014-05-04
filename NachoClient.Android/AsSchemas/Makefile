AS_SCHEMA_FILES := as_schema.py object_stack.py
AS_SCHEMA := python as_schema.py

XML_REQUEST_FILES := \
	xsd/Request/Ping.xml \

XML_RESPONSE_FILES := \
	xsd/Response/Ping.xml \

XML_COMMON_FILES := \
	xsd/common/Provision.xml \
	xsd/common/AirSyncBase.xml \
	xsd/common/Calendar.xml \
	xsd/common/Contacts.xml \
	xsd/common/Contacts2.xml \
	xsd/common/DocumentLibrary.xml \
	xsd/common/Email.xml \
	xsd/common/Email2.xml \
	xsd/common/ItemOperations.xml \
	xsd/common/RightsManagement.xml \
	xsd/common/Tasks.xml

XML_FILES := $(XML_REQUEST_FILES) $(XML_RESPONSE_FILES) $(XML_COMMON_FILES)

AS_XML_FILTER_REQUEST_FILES := $(patsubst %.xml,%Request.cs,$(notdir $(XML_REQUEST_FILES)))
AS_XML_FILTER_RESPONSE_FILES := $(patsubst %.xml,%Response.cs,$(notdir $(XML_RESPONSE_FILES)))
AS_XML_FILTER_COMMON_FILES := $(patsubst %.xml,%.cs,$(notdir $(XML_COMMON_FILES)))

OUTPUT_DIR := ../NachoCore/BackEnd/ActiveSync/AsXmlFilters

AS_XML_FILTER_FILES := $(addprefix $(OUTPUT_DIR)/AsXmlFilter,$(AS_XML_FILTER_REQUEST_FILES) $(AS_XML_FILTER_RESPONSE_FILES) $(AS_XML_FILTER_COMMON_FILES))

all: $(AS_XML_FILTER_FILES)

$(AS_XML_FILTER_FILES) : $(XML_FILES)

$(OUTPUT_DIR)/AsXmlFilterPingRequest.cs : xsd/Request/Ping.xml $(AS_SCHEMA_FILES)
	$(AS_SCHEMA) --class-suffix PingRequest --out-dir ../NachoCore/BackEnd/ActiveSync/AsXmlFilters $<

$(OUTPUT_DIR)/AsXmlFilterPingResponse.cs : xsd/Response/Ping.xml $(AS_SCHEMA_FILES)
	$(AS_SCHEMA) --class-suffix PingResponse --out-dir ../NachoCore/BackEnd/ActiveSync/AsXmlFilters $<

$(OUTPUT_DIR)/AsXmlFilter%.cs : xsd/common/%.xml $(AS_SCHEMA_FILES)
	$(AS_SCHEMA) --out-dir ../NachoCore/BackEnd/ActiveSync/AsXmlFilters $<

clean:
	rm -f $(AS_XML_FILTER_FILES)
