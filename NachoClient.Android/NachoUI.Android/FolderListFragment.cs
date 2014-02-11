using Android.OS;
using Android.Views;
using Android.Support.V4.App;
using Android.Support.V4.View;
using Android.Support.V7.App;

namespace NachoClient.AndroidClient
{
    public class FolderListFragment: Fragment
    {
        public static string ArgFolderNumber = "folder_number";

        public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var rootView = inflater.Inflate(Resource.Layout.FolderListFragment, container, false);
            var i = Arguments.GetInt(ArgFolderNumber);
            var item = "roger";
            Activity.Title = item;
            return rootView;
        }
    }
}
