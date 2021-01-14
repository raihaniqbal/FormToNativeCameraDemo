using Android.Graphics;
using Android.Media;
using Android.OS;
using Com.Dynamsoft.Dbr;
using System;
using System.Collections.Concurrent;
using System.Linq;

namespace FormsToNativeCameraDemo.Droid.Handlers
{
    public class BarcodeReaderHandler : Handler
    {
        private static BarcodeReader barcodeReader = new BarcodeReader("t0075xQAAALAGpcGXjZKjl7FDwgKCJ6236drHuNZihxgoqjzfH5OiT4DT9BbZQu/Z0I7vSGGWo0LKaIBqXk2I7qMvv8qahI5HSBNODSo5");
        private readonly int previewWidth;
        private readonly int previewHeight;

        public bool IsReady { get; set; }
        public BlockingCollection<string> DetectedCodes { get; private set; }

        public delegate void OnCodeDetectedEventHandler(int totalCodes);
        public OnCodeDetectedEventHandler CodeDetectedEventHandler { get; set; }
        public BarcodeReaderHandler(int previewWidth, int previewHeight) : base()
        {
            IsReady = true;
            this.previewWidth = previewWidth;
            this.previewHeight = previewHeight;
        }

        public BarcodeReaderHandler(Looper looper, int previewWidth, int previewHeight) : base(looper)
        {
            IsReady = true;
            this.previewWidth = previewWidth;
            this.previewHeight = previewHeight;
            DetectedCodes = new BlockingCollection<string>();
        }

        public override void HandleMessage(Message msg)
        {
            if (msg.What == 100)
            {
                Message codeDetectedMessage = new Message();
                codeDetectedMessage.What = 200;
                //msg1.Obj = "";
                try
                {
                    YuvImage image = (YuvImage)msg.Obj;
                    if (image != null)
                    {
                        int[] stridelist = image.GetStrides();
                        TextResult[] text = barcodeReader.DecodeBuffer(image.GetYuvData(), previewWidth, previewHeight, stridelist[0], EnumImagePixelFormat.IpfNv21, "");
                        if (text != null && text.Length > 0)
                        {
                            for (int i = 0; i < text.Length; i++)
                            {
                                if (!DetectedCodes.Contains(text[i].BarcodeText))
                                {
                                    DetectedCodes.Add(text[i].BarcodeText);
                                    var _mediaPlayer = MediaPlayer.Create(global::Android.App.Application.Context, Resource.Raw.ScanBeep);
                                    _mediaPlayer.Start();
                                }
                            }
                        }
                    }
                }
                catch (BarcodeReaderException e)
                {
                    //msg1.Obj = "";
                    e.PrintStackTrace();
                }

                IsReady = true;
                SendMessage(codeDetectedMessage);

            }
            else if (msg.What == 200)
            {
                //System.Console.WriteLine(msg.Obj.ToString());
                if (CodeDetectedEventHandler != null)
                    CodeDetectedEventHandler.Invoke(DetectedCodes.Count);
            }
        }
    }
}