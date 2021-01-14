using FormsToNativeCameraDemo.Services;
using System;
using System.Collections.Generic;
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
            MessagingCenter.Subscribe<App, List<string>>((App)Application.Current, "ScanComplete", async (sender, codeList) =>
            {
                if (codeList != null)
                {
                    lblScannedCodes.Text = String.Join(",", codeList);
                    await Navigation.PopAsync();
                }
            });
        }

        private async void btnStartScanning_Clicked(object sender, EventArgs e)
        {
            //_cameraSvc.LaunchActivity();
            await Navigation.PushAsync(new CameraPage());
        }
    }
}
