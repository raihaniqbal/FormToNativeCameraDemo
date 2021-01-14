using System;
using System.IO;
using Xamarin.Forms;
using Xamarin.Forms.Platform.Android;
using Android.App;
using Android.Content;
using Android.Hardware;
using Android.Views;
using Android.Graphics;
using Android.Widget;
using Android;
using Android.Content.PM;
using System.Collections.Generic;
using FormsToNativeCameraDemo;
using FormsToNativeCameraDemo.Droid.Renderers;
using Android.Support.V4.Content;
using Android.Support.V4.App;
using FormsToNativeCameraDemo.Droid.Handlers;
using Android.OS;
using Com.Dynamsoft.Dbr;

[assembly: ExportRenderer(typeof(CameraPage), typeof(CameraPageRenderer))]
namespace FormsToNativeCameraDemo.Droid.Renderers
{
    public class CameraPageRenderer : PageRenderer, TextureView.ISurfaceTextureListener, Android.Hardware.Camera.IPreviewCallback
    {
        global::Android.Hardware.Camera camera;
        global::Android.Widget.Button stopScanButton;
        global::Android.Widget.TextView txtDeviceCount;
        global::Android.Views.View view;

        Activity activity;
        CameraFacing cameraType;
        TextureView textureView;
        SurfaceTexture surfaceTexture;
        HandlerThread handlerThread;
        BarcodeReaderHandler backgroundBarcodeHandler;

        public CameraPageRenderer(Context context) : base(context)
        {
        }

        protected override void OnElementChanged(ElementChangedEventArgs<Page> e)
        {
            base.OnElementChanged(e);

            if (e.OldElement != null || Element == null)
            {
                return;
            }

            try
            {
                SetupUserInterface();
                SetupEventHandlers();
                AddView(view);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(@"			ERROR: ", ex.Message);
            }
        }

        void SetupUserInterface()
        {
            activity = this.Context as Activity;
            view = activity.LayoutInflater.Inflate(Resource.Layout.CameraLayout, this, false);
            cameraType = CameraFacing.Back;

            textureView = view.FindViewById<TextureView>(Resource.Id.cameraView);
            textureView.SurfaceTextureListener = this;
            txtDeviceCount = view.FindViewById<global::Android.Widget.TextView>(Resource.Id.txtTotalDevices);
        }

        void SetupEventHandlers()
        {
            stopScanButton = view.FindViewById<global::Android.Widget.Button>(Resource.Id.takePhotoButton);
            stopScanButton.Click += StopScanButton_Click;
        }

        protected override void OnLayout(bool changed, int l, int t, int r, int b)
        {
            base.OnLayout(changed, l, t, r, b);

            var msw = MeasureSpec.MakeMeasureSpec(r - l, MeasureSpecMode.Exactly);
            var msh = MeasureSpec.MakeMeasureSpec(b - t, MeasureSpecMode.Exactly);

            view.Measure(msw, msh);
            view.Layout(0, 0, r - l, b - t);
        }

        public void OnSurfaceTextureUpdated(SurfaceTexture surface)
        {

        }

        [Obsolete]
        public void OnSurfaceTextureAvailable(SurfaceTexture surface, int width, int height)
        {
            if (ContextCompat.CheckSelfPermission(Android.App.Application.Context, Manifest.Permission.Camera) != (int)Permission.Granted)
            {
                var requiredPermissions = new String[] { Manifest.Permission.Camera };
                var activity = Xamarin.Forms.Forms.Context as Activity;
                ActivityCompat.RequestPermissions(activity, requiredPermissions, 100);
            }
            while (ContextCompat.CheckSelfPermission(Android.App.Application.Context, Manifest.Permission.Camera) != (int)Permission.Granted)
            {
                //waiting user permission
            }

            camera = global::Android.Hardware.Camera.Open((int)cameraType);
            textureView.LayoutParameters = new FrameLayout.LayoutParams(width, height);
            surfaceTexture = surface;
            camera.SetPreviewTexture(surface);


            Android.Hardware.Camera.Parameters parameters = camera.GetParameters();
            parameters.PictureFormat = ImageFormatType.Jpeg;
            parameters.PreviewFormat = ImageFormatType.Nv21;
            if (parameters.SupportedFocusModes.Contains(Android.Hardware.Camera.Parameters.FocusModeContinuousVideo))
            {
                parameters.FocusMode = Android.Hardware.Camera.Parameters.FocusModeContinuousVideo;
            }
            //IList<Android.Hardware.Camera.Size> suportedPreviewSizes = parameters.SupportedPreviewSizes;
            //int i = 0;
            //for (i = 0; i < suportedPreviewSizes.Count; i++)
            //{
            //    if (suportedPreviewSizes[i].Width < 1300) break;
            //}
            //parameters.SetPreviewSize(suportedPreviewSizes[i].Width, suportedPreviewSizes[i].Height);
            camera.SetParameters(parameters);
            camera.SetPreviewCallback(this);

            handlerThread = new HandlerThread("background");
            handlerThread.Start();
            backgroundBarcodeHandler = new BarcodeReaderHandler(handlerThread.Looper, parameters.PreviewSize.Width, parameters.PreviewSize.Height);
            backgroundBarcodeHandler.CodeDetectedEventHandler = new BarcodeReaderHandler.OnCodeDetectedEventHandler(OnCodeDetectedHandler);

            PrepareAndStartCamera();
        }

        public bool OnSurfaceTextureDestroyed(SurfaceTexture surface)
        {
            camera.StopPreview();
            camera.Release();
            return true;
        }

        public void OnSurfaceTextureSizeChanged(SurfaceTexture surface, int width, int height)
        {
            PrepareAndStartCamera();
        }

        void PrepareAndStartCamera()
        {
            camera.StopPreview();

            var display = activity.WindowManager.DefaultDisplay;
            if (display.Rotation == SurfaceOrientation.Rotation0)
            {
                camera.SetDisplayOrientation(90);
            }

            if (display.Rotation == SurfaceOrientation.Rotation270)
            {
                camera.SetDisplayOrientation(180);
            }

            camera.StartPreview();
        }

        [Obsolete]
        public void OnPreviewFrame(byte[] data, Android.Hardware.Camera camera)
        {
            Android.Hardware.Camera.Parameters parameters = camera.GetParameters();
            //Console.WriteLine($"Width: {parameters.PreviewSize.Width}, Height: {parameters.PreviewSize.Height}");

            var yuvImage = new YuvImage(data, ImageFormatType.Nv21, parameters.PreviewSize.Width, parameters.PreviewSize.Height, null);
            var stride = yuvImage.GetStrides();

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

        #region Event Handlers
        private void StopScanButton_Click(object sender, EventArgs e)
        {

        }
        private void OnCodeDetectedHandler(int totalCodes)
        {
            //txtTotalBarcodesFound.Text = string.Format("Devices Found: {0}", totalCodes);
            if(totalCodes > 0)
                Console.WriteLine(string.Format("Devices Found: {0}", totalCodes));
        }

        #endregion
    }
}