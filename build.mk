# Build / clean everything

all:
	make -C ../TokenAutoComplete -f ../NachoClientX/TokenAutoComplete.mk
	make -C ../SwipeViewBinding
	make -C ../UIImageEffects
	make -C ../SWRevealViewControllerBinding
	make -C ../NachoPlatformBinding
	make -C ../NachoUIMonitorBinding
	make -C ../DnDns/SourceCode/DnDns -f ../../../NachoClientX/DnDns.mk
	make -C ../DDay-iCal-Xamarin
	make -C ../MailKit SOLUTION=MailKit.Mobile.sln
	make -C native.iOS
	make -C native.Android
	make

clean:
	make -C ../SwipeViewBinding clean
	make -C ../UIImageEffects clean
	make -C ../SWRevealViewControllerBinding clean
	make -C ../ios-openssl clean
	make -C ../NachoPlatformBinding clean
	make -C ../NachoUIMonitorBinding clean
	make -C ../DnDns/SourceCode/DnDns -f ../../../NachoClientX/DnDns.mk clean
	make -C ../DDay-iCal-Xamarin clean
	make -C ../MailKit SOLUTION=MailKit.Mobile.sln clean
	make -C native.iOS clean
	make -C native.Android clean
	make clean
