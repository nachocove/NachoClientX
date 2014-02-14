all:
	/Applications/Xamarin\ Studio.app/Contents/MacOS/mdtool build BouncyCastle.Mobile.sln

clean:
	/Applications/Xamarin\ Studio.app/Contents/MacOS/mdtool build --target:Clean BouncyCastle.Mobile.sln
	rm -fr ./crypto/bin
	rm -fr ./crypto/obj
	rm -fr ./crypto/doc
