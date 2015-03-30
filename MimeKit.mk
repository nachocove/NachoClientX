all:
	/Applications/Xamarin\ Studio.app/Contents/MacOS/mdtool build MimeKit.Mobile.sln

clean:
	/Applications/Xamarin\ Studio.app/Contents/MacOS/mdtool build --target:Clean MimeKit.Mobile.sln
	rm -fr ./crypto/bin
	rm -fr ./crypto/obj
	rm -fr ./crypto/doc
	rm -fr MimeKit/bin MimeKit/obj
