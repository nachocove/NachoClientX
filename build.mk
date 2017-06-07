# Build / clean everything

all:
	make -C ../TokenAutoComplete -f ../NachoClientX/TokenAutoComplete.mk
	make -C ../NachoPlatformBinding
	make -C ../NachoUIMonitorBinding
	make -C ../DnDns/SourceCode/DnDns -f ../../../NachoClientX/DnDns.mk
	make -C ../DDay-iCal-Xamarin
	make -C ../MailKit SOLUTION=MailKit.Mobile.sln
	make -C native.iOS
	make -C native.Android
	make -C native.Mac
	make

clean:
	make -C ../ios-openssl clean
	make -C ../NachoPlatformBinding clean
	make -C ../NachoUIMonitorBinding clean
	make -C ../DnDns/SourceCode/DnDns -f ../../../NachoClientX/DnDns.mk clean
	make -C ../DDay-iCal-Xamarin clean
	make -C ../MailKit SOLUTION=MailKit.Mobile.sln clean
	make -C native.iOS clean
	make -C native.Android clean
	make -C native.Mac clean
	make clean
