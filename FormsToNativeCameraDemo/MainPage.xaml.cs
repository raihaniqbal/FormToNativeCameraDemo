using FormsToNativeCameraDemo.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace FormsToNativeCameraDemo
{
    public partial class MainPage : ContentPage
    {
        private readonly ICameraActivityService _cameraSvc;
        public MainPage()
        {
            InitializeComponent();
            _cameraSvc = DependencyService.Get<ICameraActivityService>();
            MessagingCenter.Subscribe<App, List<string>>((App)Application.Current, "ScanComplete", (sender, codeList) =>
            {
                if (codeList != null)
                    lblScannedCodes.Text = String.Join(",", codeList);
                //_cameraSvc.StopActivity();
            });
        }

        private void btnStartScanning_Clicked(object sender, EventArgs e)
        {
            _cameraSvc.LaunchActivity();
        }
    }
}
