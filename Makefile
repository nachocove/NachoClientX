include build_env.mk

ifeq ("$(HAS_CLI_CONFIG)","No")
all:
	$(MDTOOL) build --target:Build NachoClient.sln

clean:
	$(MDTOOL) build --target:Clean NachoClient.sln
else
all:
	$(MDTOOL) build --target:Build --configuration:"$(CONFIG)" NachoClient.sln

clean:
	$(MDTOOL) build --target:Clean --configuration:"$(CONFIG)" NachoClient.sln
endif

release:
	./scripts/configure_ios.py ./NachoClient.iOS/Info.plist
	$(MDTOOL) build --target:Build --configuration:"Ad-Hoc|iPhone" NachoClient.sln
