include build_env.mk

TOP:=..

mk_bin_obj_dirs=$1/bin $1/obj

PROJECTS:= \
	NachoClient.Andrioid \
	NachoClient.iOS \
	Test.Android \
	Test.iOS \
	$(TOP)/aws-sdk-xamarin/AWS.XamarinSDK/AWSSDK_iOS \
	$(TOP)/MobileHtmlAgilityPack \
	IndexLib \
	$(TOP)/lucene.net-3.0.3/Lucene.Net \
	$(TOP)/lucene.net-3.0.3/Lucene.Net.Android \
	$(TOP)/lucene.net-3.0.3/Lucene.Net.iOS \

DIRS:= \
	$(foreach dir,$(PROJECTS),$(call mk_bin_obj_dirs,$(dir))) \
	Test.Android/lib \

ifeq ("$(HAS_CLI_CONFIG)","No")
all:
	$(MDTOOL) build --target:Build NachoClient.sln

clean:
	$(MDTOOL) build --target:Clean NachoClient.sln
	@sh -c "for d in $(DIRS); do echo Cleaning \$$d; rm -fr \$$d; done"
else
all:
	$(MDTOOL) build --target:Build --configuration:"$(CONFIG)" NachoClient.sln

clean:
	$(MDTOOL) build --target:Clean --configuration:"$(CONFIG)" NachoClient.sln
	@sh -c "for d in $(DIRS); do echo Cleaning \$$d; rm -fr \$$d; done"
endif

release:
	(cd ./NachoClient.Android; ../scripts/configure_android.py ./Properties/AndroidManifest.xml)
	(cd ./NachoClient.iOS; ../scripts/configure_ios.py ./Info.plist)
	$(MDTOOL) build --target:Build --configuration:"Ad-Hoc|iPhone" NachoClient.sln
	$(XBUILD) /p:Configuration=Release NachoClient.Android/NachoClient.Android.csproj /t:SignAndroidPackage

appstore:
	(cd ./NachoClient.Android; ../scripts/configure_android.py ./Properties/AndroidManifest.xml)
	(cd ./NachoClient.iOS; ../scripts/configure_ios.py ./Info.plist)
	$(MDTOOL) build --target:Build --configuration:"AppStore|iPhone" NachoClient.sln
	$(XBUILD) /p:Configuration=Release NachoClient.Android/NachoClient.Android.csproj /t:SignAndroidPackage
