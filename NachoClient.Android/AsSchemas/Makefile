AS_SCHEMA_FILES := as_schema.py as_xml.py object_stack.py output.py
AS_SCHEMA := python as_schema.py

include namespaces.mk

AS_XML_FILTER_REQUEST_FILES := $(addsuffix Request.cs,$(REQUEST_NAMESPACES))
AS_XML_FILTER_RESPONSE_FILES := $(addsuffix Response.cs,$(RESPONSE_NAMESPACES))
AS_XML_FILTER_COMMON_FILES := $(addsuffix .cs,$(COMMON_NAMESPACES))

OUTPUT_DIR := ../NachoCore/BackEnd/ActiveSync/AsXmlFilters

AS_XML_FILTER_FILES := $(addprefix $(OUTPUT_DIR)/AsXmlFilter,$(AS_XML_FILTER_REQUEST_FILES) $(AS_XML_FILTER_RESPONSE_FILES) $(AS_XML_FILTER_COMMON_FILES))

all: $(AS_XML_FILTER_FILES)

$(OUTPUT_DIR)/AsXmlFilter%Request.cs : xsd/Request/%.xml $(AS_SCHEMA_FILES)
	$(AS_SCHEMA) --class-suffix $(basename $(notdir $<))Request --out-dir $(OUTPUT_DIR) $<

$(OUTPUT_DIR)/AsXmlFilter%Response.cs : xsd/Response/%.xml $(AS_SCHEMA_FILES)
	$(AS_SCHEMA) --class-suffix $(basename $(notdir $<))Response --out-dir $(OUTPUT_DIR) $<

$(OUTPUT_DIR)/AsXmlFilter%.cs : xsd/common/%.xml $(AS_SCHEMA_FILES)
	$(AS_SCHEMA) --out-dir $(OUTPUT_DIR) $<

clean:
	rm -f $(AS_XML_FILTER_FILES)
