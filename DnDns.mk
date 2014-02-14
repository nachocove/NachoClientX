all:
	/Applications/Xamarin\ Studio.app/Contents/MacOS/mdtool build -c:Release DnDns-Xamarin.sln

clean:
	/Applications/Xamarin\ Studio.app/Contents/MacOS/mdtool build --target:Clean DnDns-Xamarin.sln

