AS_SCHEMA_FILES := as_schema.py object_stack.py
AS_SCHEMA := python as_schema.py

XSD_FILES := \
	xsd/Request/Ping.xsd \
	xsd/Response/Ping.xsd \
	xsd/common/Provision.xsd \

#	xsd/common/AirSyncBase.xsd \
#	xsd/common/Calendar.xsd \
#	xsd/common/Contacts.xsd \
#	xsd/common/Contacts2.xsd \
#	xsd/common/DocumentLibrary.xsd \
#	xsd/common/Email.xsd \
#	xsd/common/Email2.xsd \
#	xsd/common/ItemOperations.xsd \
#	xsd/common/RightsManagement.xsd \
#	xsd/common/Tasks.xsd

AS_XML_FILTER_FILES := \
	AsXmlFilterPingRequest.cs \
	AsXmlFilterPingResponse.cs \

all: $(AS_XML_FILTER_FILES)

$(AS_XML_FILTER_FILES) : $(patsubst %.xsd,%.xml,$(XSD_FILES))

%.xml : %.xsd as_xsd.py object_stack.py
	python as_xsd.py $<

AsXmlFilterPingRequest.cs : xsd/Request/Ping.xml $(AS_SCHEMA_FILES)
	$(AS_SCHEMA) $< PingRequest

AsXmlFilterPingResponse.cs : xsd/Response/Ping.xml $(AS_SCHEMA_FILES)
	$(AS_SCHEMA) $< PingResponse

clean:
	rm *.cs