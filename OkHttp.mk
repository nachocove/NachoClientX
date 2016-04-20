all:
	/Applications/Xamarin\ Studio.app/Contents/MacOS/mdtool build -c:Release OkHttp.sln

clean:
	/Applications/Xamarin\ Studio.app/Contents/MacOS/mdtool build --target:Clean OkHttp.sln
	rm -fr OkHttp/bin OkHttp/obj


