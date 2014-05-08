# This is the list of namespaces that are produced from .xsd into .xml
# and then from .xml to .cs

REQUEST_NAMESPACES := \
	AirSync \
	ComposeMail \
	FolderHierarchy \
	GetItemEstimate \
	ItemOperations \
	MeetingResponse \
	Move \
	Ping \
	Provision \
	ResolveRecipients \
	Search \
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
	GAL \
	RightsManagement \
	Tasks \

# These common namespace do not produce its own XML config files because
# they are included into requests / responses
#
# AirSync
# ComposeMail
# FolderHierarchy
# ItemOperations
# Provision
# Settings

