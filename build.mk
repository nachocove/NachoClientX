# Build / clean everything

all:
	make -C ../SwipeViewBinding
	make -C ../UIImageEffects
	make -C ../SWRevealViewControllerBinding
	make -C ../ios-openssl
	make -C ../NachoPlatformBinding
	make -C ../NachoUIMonitorBinding
	make -C ../bc-csharp -f ../NachoClientX/bc-csharp.mk
	make -C ../MimeKit -f ../NachoClientX/MimeKit.mk
	make -C ../DnDns/SourceCode/DnDns -f ../../../NachoClientX/DnDns.mk
	make -C ../DDay-iCal-Xamarin
	make -C ../ModernHttpClient
	make -C ../ios-openssl
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
	make -C ../bc-csharp -f ../NachoClientX/bc-csharp.mk clean
	make -C ../MimeKit -f ../NachoClientX/MimeKit.mk clean
	make -C ../DnDns/SourceCode/DnDns -f ../../../NachoClientX/DnDns.mk clean
	make -C ../DDay-iCal-Xamarin clean
	make -C ../ModernHttpClient clean
	make -C native.iOS clean
	make -C native.Android clean
	make clean
