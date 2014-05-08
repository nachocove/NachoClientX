AS_SCHEMA_FILES := as_schema.py as_xml.py object_stack.py output.py
AS_SCHEMA := python as_schema.py

XML_REQUEST_FILES := \
	xsd/Request/AirSync.xml \
	xsd/Request/ComposeMail.xml \
	xsd/Request/FolderHierarchy.xml \
	xsd/Request/Ping.xml \
	xsd/Request/Provision.xml \
	xsd/Request/Settings.xml \

XML_RESPONSE_FILES := \
	xsd/Response/AirSync.xml \
	xsd/Response/ComposeMail.xml \
	xsd/Response/FolderHierarchy.xml \
	xsd/Response/Ping.xml \
	xsd/Response/Provision.xml \
	xsd/Response/Settings.xml \

XML_COMMON_FILES := \
	xsd/common/AirSyncBase.xml \
	xsd/common/Calendar.xml \
	xsd/common/Contacts.xml \
	xsd/common/Contacts2.xml \
	xsd/common/DocumentLibrary.xml \
	xsd/common/Email.xml \
	xsd/common/Email2.xml \
	xsd/common/RightsManagement.xml \
	xsd/common/Tasks.xml \

XML_FILES := $(XML_REQUEST_FILES) $(XML_RESPONSE_FILES) $(XML_COMMON_FILES)

AS_XML_FILTER_REQUEST_FILES := $(patsubst %.xml,%Request.cs,$(notdir $(XML_REQUEST_FILES)))
AS_XML_FILTER_RESPONSE_FILES := $(patsubst %.xml,%Response.cs,$(notdir $(XML_RESPONSE_FILES)))
AS_XML_FILTER_COMMON_FILES := $(patsubst %.xml,%.cs,$(notdir $(XML_COMMON_FILES)))

OUTPUT_DIR := ../NachoCore/BackEnd/ActiveSync/AsXmlFilters

AS_XML_FILTER_FILES := $(addprefix $(OUTPUT_DIR)/AsXmlFilter,$(AS_XML_FILTER_REQUEST_FILES) $(AS_XML_FILTER_RESPONSE_FILES) $(AS_XML_FILTER_COMMON_FILES))

all: $(AS_XML_FILTER_FILES)

$(AS_XML_FILTER_FILES) : $(XML_FILES)

$(OUTPUT_DIR)/AsXmlFilterAirSyncRequest.cs : xsd/Request/AirSync.xml $(AS_SCHEMA_FILES)
	$(AS_SCHEMA) --class-suffix AirSyncRequest --out-dir ../NachoCore/BackEnd/ActiveSync/AsXmlFilters $<

$(OUTPUT_DIR)/AsXmlFilterAirSyncResponse.cs : xsd/Response/AirSync.xml $(AS_SCHEMA_FILES)
	$(AS_SCHEMA) --class-suffix AirSyncResponse --out-dir ../NachoCore/BackEnd/ActiveSync/AsXmlFilters $<

$(OUTPUT_DIR)/AsXmlFilterComposeMailRequest.cs : xsd/Request/ComposeMail.xml $(AS_SCHEMA_FILES)
	$(AS_SCHEMA) --class-suffix ComposeMailRequest --out-dir ../NachoCore/BackEnd/ActiveSync/AsXmlFilters $<

$(OUTPUT_DIR)/AsXmlFilterComposeMailResponse.cs : xsd/Response/ComposeMail.xml $(AS_SCHEMA_FILES)
	$(AS_SCHEMA) --class-suffix ComposeMailResponse --out-dir ../NachoCore/BackEnd/ActiveSync/AsXmlFilters $<

$(OUTPUT_DIR)/AsXmlFilterFolderHierarchyRequest.cs : xsd/Request/FolderHierarchy.xml $(AS_SCHEMA_FILES)
	$(AS_SCHEMA) --class-suffix FolderHierarchyRequest --out-dir ../NachoCore/BackEnd/ActiveSync/AsXmlFilters $<

$(OUTPUT_DIR)/AsXmlFilterFolderHierarchyResponse.cs : xsd/Response/FolderHierarchy.xml $(AS_SCHEMA_FILES)
	$(AS_SCHEMA) --class-suffix FolderHierarchyResponse --out-dir ../NachoCore/BackEnd/ActiveSync/AsXmlFilters $<

$(OUTPUT_DIR)/AsXmlFilterPingRequest.cs : xsd/Request/Ping.xml $(AS_SCHEMA_FILES)
	$(AS_SCHEMA) --class-suffix PingRequest --out-dir ../NachoCore/BackEnd/ActiveSync/AsXmlFilters $<

$(OUTPUT_DIR)/AsXmlFilterPingResponse.cs : xsd/Response/Ping.xml $(AS_SCHEMA_FILES)
	$(AS_SCHEMA) --class-suffix PingResponse --out-dir ../NachoCore/BackEnd/ActiveSync/AsXmlFilters $<

$(OUTPUT_DIR)/AsXmlFilterProvisionRequest.cs : xsd/Request/Provision.xml $(AS_SCHEMA_FILES)
	$(AS_SCHEMA) --class-suffix ProvisionRequest --out-dir ../NachoCore/BackEnd/ActiveSync/AsXmlFilters $<

$(OUTPUT_DIR)/AsXmlFilterProvisionResponse.cs : xsd/Response/Provision.xml $(AS_SCHEMA_FILES)
	$(AS_SCHEMA) --class-suffix ProvisionResponse --out-dir ../NachoCore/BackEnd/ActiveSync/AsXmlFilters $<

$(OUTPUT_DIR)/AsXmlFilterSettingsRequest.cs : xsd/Request/Settings.xml $(AS_SCHEMA_FILES)
	$(AS_SCHEMA) --class-suffix SettingsRequest --out-dir ../NachoCore/BackEnd/ActiveSync/AsXmlFilters $<

$(OUTPUT_DIR)/AsXmlFilterSettingsResponse.cs : xsd/Response/Settings.xml $(AS_SCHEMA_FILES)
	$(AS_SCHEMA) --class-suffix SettingsResponse --out-dir ../NachoCore/BackEnd/ActiveSync/AsXmlFilters $<

$(OUTPUT_DIR)/AsXmlFilter%.cs : xsd/common/%.xml $(AS_SCHEMA_FILES)
	$(AS_SCHEMA) --out-dir ../NachoCore/BackEnd/ActiveSync/AsXmlFilters $<

clean:
	rm -f $(AS_XML_FILTER_FILES)
