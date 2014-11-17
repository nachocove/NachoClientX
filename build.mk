# Build / clean everything

all:
	make -C ../Parse
	make -C ../Crashlytics
	make -C ../iCarouselBinding
	make -C ../SwipeViewBinding
	make -C ../UIImageEffects
	make -C ../SWRevealViewControllerBinding
	make -C ../MCSwipeTableViewCellBinding
	make -C ../NachoPlatformBinding
	make -C ../NachoUIMonitorBinding
	make -C ../bc-csharp -f ../NachoClientX/bc-csharp.mk
	make -C ../MimeKit -f ../NachoClientX/MimeKit.mk
	make -C ../DnDns/SourceCode/DnDns -f ../../../NachoClientX/DnDns.mk
	make -C ../DDay-iCal-Xamarin
	make -C native.iOS
	make -C native.Android
	make

clean:
	make -C ../Parse
	make -C ../Crashlytics clean
	make -C ../iCarouselBinding clean
	make -C ../SwipeViewBinding clean
	make -C ../UIImageEffects clean
	make -C ../SWRevealViewControllerBinding clean
	make -C ../MCSwipeTableViewCellBinding clean
	make -C ../NachoPlatformBinding clean
	make -C ../NachoUIMonitorBinding clean
	make -C ../bc-csharp -f ../NachoClientX/bc-csharp.mk clean
	make -C ../MimeKit -f ../NachoClientX/MimeKit.mk clean
	make -C ../DnDns/SourceCode/DnDns -f ../../../NachoClientX/DnDns.mk clean
	make -C ../DDay-iCal-Xamarin clean
	make -C native.iOS clean
	make -C native.Android clean
	make clean
