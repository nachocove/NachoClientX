all:
	/Applications/Xamarin\ Studio.app/Contents/MacOS/mdtool build NachoClient.sln

clean:
	/Applications/Xamarin\ Studio.app/Contents/MacOS/mdtool build --target:Clean NachoClient.sln
