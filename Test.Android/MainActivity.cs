using System.Reflection;
using Android.App;
using Android.OS;
using Android.Content;
using NachoPlatform;

using Xamarin.Android.NUnitLite;
using NachoClient.AndroidClient;

namespace Test.Android
{
    [Activity (Label = "Test.Android", MainLauncher = true)]
    public class MainActivity : TestSuiteActivity
    {
        protected override void OnCreate (Bundle bundle)
        {
            // tests can be inside the main assembly
            AddTest (Assembly.GetExecutingAssembly ());
            // or in any reference assemblies
            // AddTest (typeof (Your.Library.TestClass).Assembly);

            // Once you called base.OnCreate(), you cannot add more assemblies.
            base.OnCreate (bundle);

            NachoPlatform.Assets.AndroidAssetManager = Assets;
            MainApplication._instance = this.Application;
        }
    }
}

