﻿using System;
using System.Windows.Media.Imaging;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;

namespace WpfHandGazeDistance.Models
{
    /// <summary>
    /// Contains a string with the file path of the video sowurce as well as a Emgu VideoCapture class
    /// to contain the video.
    /// </summary>
    public class Video
    {
        #region Private Properties
        
        private VideoCapture _capture;

        #endregion

        #region Public Properties

        public Image ThumbnailImage { get; set; }

        public float Fps => (float)_capture.GetCaptureProperty(CapProp.Fps);

        public int FrameCount => (int)Math.Floor(_capture.GetCaptureProperty(CapProp.FrameCount));

        #endregion

        #region Constructor

        /// <summary>
        /// The constructor simply calls the VideoSource property setter which instantiates a new video capture.
        /// </summary>
        /// <param name="videoPath"></param>
        public Video(string videoPath = null)
        {
            if (videoPath == null) return;

            _capture = new VideoCapture(videoPath);
            ThumbnailImage = GetImageFrame();
        }

        #endregion

        #region Public Members

        public Mat GetMatFrame()
        {
            return _capture.QueryFrame();
        }

        public Image GetImageFrame()
        {
            Mat frameMat = GetMatFrame();
            return frameMat != null ? new Image(frameMat) : null;
        }

        public Image<Bgr, byte> GetBgrImageFrame()
        {
            Mat frameMat = GetMatFrame();
            return frameMat?.ToImage<Bgr, byte>();
        }

        public BitmapSource GetBitmapFrame()
        {
            Mat frameMat = GetMatFrame();
            return frameMat != null ? new Image(frameMat).BitMapImage : null;
        }

        #endregion
    }
}
