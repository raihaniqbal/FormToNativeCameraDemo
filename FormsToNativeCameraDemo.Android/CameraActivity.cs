using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.OS;
using Android.Runtime;
using Android.Support.V4.App;
using Android.Views;
using Android.Widget;
using Com.Dynamsoft.Dbr;
using FormsToNativeCameraDemo.Droid;
using FormsToNativeCameraDemo.Droid.Handlers;
using FormsToNativeCameraDemo.Services;
using System.Collections.Generic;
using Xamarin.Forms;
using System.Linq;

[assembly: Dependency(typeof(CameraActivity))]
namespace FormsToNativeCameraDemo.Droid
{
    [Activity(Label = "CameraActivity")]
    public class CameraActivity : Activity, ICameraActivityService, Android.Hardware.Camera.IPreviewCallback, ISurfaceHolderCallback, Android.Support.V4.App.ActivityCompat.IOnRequestPermissionsResultCallback
    {
        private Android.Hardware.Camera camera;
        private SurfaceView surface = null;
        private static TextView txtTotalBarcodesFound;
        private static int previewWidth;
        private static int previewHeight;
        private static YuvImage yuvImage;
        private static int[] stride;
        private static bool fromBack = false;
        public const int REQUEST_CAMERA_PERMISSION = 1;
        private HandlerThread handlerThread;
        private BarcodeReaderHandler backgroundBarcodeHandler;

        public void LaunchActivity()
        {
            Activity activity = Forms.Context as Activity;
            var intent = new Intent(Forms.Context, typeof(CameraActivity));
            activity.StartActivity(intent);
        }

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.camera_activity);
            surface = FindViewById<SurfaceView>(Resource.Id.sv_surfaceView);
            txtTotalBarcodesFound = FindViewById<TextView>(Resource.Id.txtTotalDevices);
            var btnStopScan = FindViewById<Android.Widget.ImageButton>(Resource.Id.btnStopScan);
            btnStopScan.Click += BtnStopScan_Click;
            var holder = surface.Holder;
            holder.AddCallback(this);
        }

        private void BtnStopScan_Click(object sender, System.EventArgs e)
        {
            MessagingCenter.Send((App)Xamarin.Forms.Application.Current, "ScanComplete", backgroundBarcodeHandler.DetectedCodes.ToList());
            Finish();
        }

        protected override void OnResume()
        {
            base.OnResume();
            if (fromBack)
            {
                surface.Holder.AddCallback(this);
                fromBack = false;
            }
        }

        protected override void OnPause()
        {
            fromBack = true;
            base.OnPause();
        }

        private void OpenCamera()
        {
            if (CheckSelfPermission(Manifest.Permission.Camera) != Permission.Granted)
            {
                RequestCameraPermission();
                return;
            }


            camera = Android.Hardware.Camera.Open();
            Android.Hardware.Camera.Parameters parameters = camera.GetParameters();
            parameters.PictureFormat = ImageFormatType.Jpeg;
            parameters.PreviewFormat = ImageFormatType.Nv21;
            if (parameters.SupportedFocusModes.Contains(Android.Hardware.Camera.Parameters.FocusModeContinuousVideo))
            {
                parameters.FocusMode = Android.Hardware.Camera.Parameters.FocusModeContinuousVideo;
            }
            IList<Android.Hardware.Camera.Size> suportedPreviewSizes = parameters.SupportedPreviewSizes;
            int i = 0;
            for (i = 0; i < suportedPreviewSizes.Count; i++)
            {
                if (suportedPreviewSizes[i].Width < 1300) break;
            }
            parameters.SetPreviewSize(suportedPreviewSizes[i].Width, suportedPreviewSizes[i].Height);
            camera.SetParameters(parameters);
            camera.SetDisplayOrientation(90);
            camera.SetPreviewCallback(this);
            camera.SetPreviewDisplay(surface.Holder);
            camera.StartPreview();
            //Get camera width
            previewWidth = parameters.PreviewSize.Width;
            //Get camera height
            previewHeight = parameters.PreviewSize.Height;

            //Resize SurfaceView Size
            float scaledHeight = previewWidth * 1.0f * surface.Width / previewHeight;
            float prevHeight = surface.Height;
            ViewGroup.LayoutParams lp = surface.LayoutParameters;
            lp.Width = surface.Width;
            lp.Height = (int)scaledHeight;
            surface.LayoutParameters = lp;
            surface.Top = (int)((prevHeight - scaledHeight) / 2);
            surface.DrawingCacheEnabled = true;

            handlerThread = new HandlerThread("background");
            handlerThread.Start();
            backgroundBarcodeHandler = new BarcodeReaderHandler(handlerThread.Looper, previewWidth, previewHeight);
            backgroundBarcodeHandler.CodeDetectedEventHandler = new BarcodeReaderHandler.OnCodeDetectedEventHandler(OnCodeDetectedHandler);
        }

        private void OnCodeDetectedHandler(int totalCodes)
        {
            txtTotalBarcodesFound.Text = string.Format("Devices Found: {0}", totalCodes);
        }

        private void RequestCameraPermission()
        {
            if (ActivityCompat.ShouldShowRequestPermissionRationale(this, Manifest.Permission.Camera))
            {
                ActivityCompat.RequestPermissions(this,
                                new string[] { Manifest.Permission.Camera }, REQUEST_CAMERA_PERMISSION);
            }
            else
            {
                ActivityCompat.RequestPermissions(this, new string[] { Manifest.Permission.Camera },
                        REQUEST_CAMERA_PERMISSION);
            }
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Permission[] grantResults)
        {
            switch (requestCode)
            {
                case REQUEST_CAMERA_PERMISSION:
                    if (grantResults.Length > 0 && grantResults[0] == Permission.Granted)
                        OpenCamera();
                    else
                        Toast.MakeText(ApplicationContext, "This App need permission to access camera.", ToastLength.Long).Show();
                    return;
            }
        }

        public void SurfaceChanged(ISurfaceHolder holder, [GeneratedEnum] Format format, int width, int height)
        {
            
        }

        public void SurfaceCreated(ISurfaceHolder holder)
        {
            OpenCamera();
        }

        public void SurfaceDestroyed(ISurfaceHolder holder)
        {
            holder.RemoveCallback(this);
            if (camera != null)
            {
                camera.SetPreviewCallback(null);
                camera.StopPreview();
                camera.Release();
                camera = null;
            }
            if (handlerThread != null)
            {
                handlerThread.QuitSafely();
                handlerThread.Join();
                handlerThread = null;
            }
            backgroundBarcodeHandler = null;
        }

        public void OnPreviewFrame(byte[] data, Android.Hardware.Camera camera)
        {
            try
            {
                System.Console.WriteLine("start create Image");
                yuvImage = new YuvImage(data, ImageFormatType.Nv21,
                        previewWidth, previewHeight, null);

                stride = yuvImage.GetStrides();

                try
                {
                    if (backgroundBarcodeHandler.IsReady)
                    {
                        if (backgroundBarcodeHandler != null)
                        {
                            backgroundBarcodeHandler.IsReady = false;
                            Message msg = new Message();
                            msg.What = 100;
                            msg.Obj = yuvImage;
                            backgroundBarcodeHandler.SendMessage(msg);
                        }

                    }
                }
                catch (BarcodeReaderException e)
                {
                    e.PrintStackTrace();
                }

            }
            catch (System.IO.IOException)
            {


            }
        }
    }
}