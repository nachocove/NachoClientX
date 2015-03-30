all:
	/Applications/Xamarin\ Studio.app/Contents/MacOS/mdtool build -c:Release DnDns-Xamarin.sln

clean:
	/Applications/Xamarin\ Studio.app/Contents/MacOS/mdtool build --target:Clean DnDns-Xamarin.sln
	rm -fr DnDns.Android/bin DnDns.Android/obj
	rm -fr DnDns.iOS/bin DnDns.iOS/obj


