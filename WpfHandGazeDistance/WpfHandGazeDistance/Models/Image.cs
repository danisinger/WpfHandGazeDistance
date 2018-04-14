﻿using System.Windows.Media.Imaging;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using WpfHandGazeDistance.Helpers;

namespace WpfHandGazeDistance.Models
{
    /// <summary>
    /// This could at some point become the parent class of BgrImage and HsvImage etc.
    /// </summary>
    public class Image
    {
        public Image<Bgr, byte> BgrImage { get; set; }

        public Image<Gray, byte> GrayImage { get; set; }

        public BitmapSource BitMapImage { get; set; }

        public Image(IImage inputImage)
        {
            BgrImage = new Image<Bgr, byte>(inputImage.Bitmap);
            GrayImage = new Image<Gray, byte>(inputImage.Bitmap);
            BitMapImage = BitMapConverter.ToBitmapSource(inputImage);
        }

        public void Resize(double scale)
        {
            BgrImage.Resize(scale, Inter.Linear);
            GrayImage.Resize(scale, Inter.Linear);
            BitMapImage = BitMapConverter.ToBitmapSource(BgrImage);
        }
    }
}
