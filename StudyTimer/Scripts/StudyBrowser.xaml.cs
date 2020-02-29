using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace StudyTimer.Scripts
{
    /// <summary>
    /// Interaction logic for StudyBrowser.xaml
    /// </summary>
    public partial class StudyBrowser : Window
    {
		List<string> prohibitedKeywords = new List<string>() { "youtube", "reddit" };
        public StudyBrowser()
        {
            InitializeComponent();
			wbSample.Navigated += new NavigatedEventHandler(wbMain_Navigated);
			wbSample.Navigate("http://www.google.com");
		}

		private void txtUrl_KeyUp(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Enter)
			{
				if (!txtUrl.Text.StartsWith("http"))
					txtUrl.Text = "http://www." + txtUrl.Text;
				wbSample.Navigate(txtUrl.Text);
			}
		}

		private void wbSample_Navigating(object sender, System.Windows.Navigation.NavigatingCancelEventArgs e)
		{
			foreach(string s in prohibitedKeywords) 
				if (e.Uri.OriginalString.ToLower().Contains(s))
				{
					e.Cancel = true;
					MessageBox.Show("This web site is prohibited", "Prohibited", MessageBoxButton.OK, MessageBoxImage.Exclamation);
					break;
				}
			txtUrl.Text = e.Uri.OriginalString;
		}

		private void BrowseBack_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = ((wbSample != null) && (wbSample.CanGoBack));
		}

		private void BrowseBack_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			wbSample.GoBack();
		}

		private void BrowseForward_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = ((wbSample != null) && (wbSample.CanGoForward));
		}

		private void BrowseForward_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			wbSample.GoForward();
		}

		private void GoToPage_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = true;
		}

		private void GoToPage_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			wbSample.Navigate(txtUrl.Text);
		}

		void wbMain_Navigated(object sender, NavigationEventArgs e)
		{
			SetSilent(wbSample, true); // make it silent
		}

		private void button1_Click(object sender, RoutedEventArgs e)
		{
			wbSample.Navigate(new Uri("... some url..."));
		}

		public static void SetSilent(WebBrowser browser, bool silent)
		{
			if (browser == null)
				throw new ArgumentNullException("browser");

			// get an IWebBrowser2 from the document
			IOleServiceProvider sp = browser.Document as IOleServiceProvider;
			if (sp != null)
			{
				Guid IID_IWebBrowserApp = new Guid("0002DF05-0000-0000-C000-000000000046");
				Guid IID_IWebBrowser2 = new Guid("D30C1661-CDAF-11d0-8A3E-00C04FC9E26E");

				object webBrowser;
				sp.QueryService(ref IID_IWebBrowserApp, ref IID_IWebBrowser2, out webBrowser);
				if (webBrowser != null)
				{
					webBrowser.GetType().InvokeMember("Silent", BindingFlags.Instance | BindingFlags.Public | BindingFlags.PutDispProperty, null, webBrowser, new object[] { silent });
				}
			}
		}


		[ComImport, Guid("6D5140C1-7436-11CE-8034-00AA006009FA"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
		private interface IOleServiceProvider
		{
			[PreserveSig]
			int QueryService([In] ref Guid guidService, [In] ref Guid riid, [MarshalAs(UnmanagedType.IDispatch)] out object ppvObject);
		}
	}
}
