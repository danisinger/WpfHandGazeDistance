﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using WpfHandGazeDistance.Models;

namespace WpfHandGazeDistance.Helpers
{
    public class HandDetector
    {
        #region Public Properties

        public BeGazeData BeGazeData { get; }

        public Video Video { get; }

        public HgdData HgdData { get; }

        #endregion

        #region Public Members

        public HandDetector(BeGazeData beGazeData, Video video)
        {
            BeGazeData = beGazeData;
            Video = video;
            HgdData = new HgdData();
        }

        /// <summary>
        /// This function will segment, erode and filter a BGR image to find the hands
        /// and then return the hand contours as white contours on a black background.
        /// </summary>
        /// <param name="inputImage">A standard BGR image.</param>
        /// <returns>A grayscale image with the hands as white.</returns>
        public static Image<Gray, byte> AnalyseImage(Image<Bgr, byte> inputImage)
        {
            Image<Gray, byte> outputImage = new Image<Gray, byte>(inputImage.Size);
            VectorOfVectorOfPoint handContours = FindHands(inputImage);
            CvInvoke.DrawContours(outputImage, handContours, -1, new MCvScalar(255), -1);

            return outputImage;
        }

        /// <summary>
        /// This function will segment and erode a BGR image into a grayscale image with the hands
        /// and then figure out the two largest contours above a certain size within that image.
        /// By querying handContours.Size you can figure out the number of hands in the image.
        /// </summary>
        /// <param name="inputImage">Standard BGR image</param>
        /// <returns>Vector of points with the two largest contours.</returns>
        public static VectorOfVectorOfPoint FindHands(Image<Bgr, byte> inputImage)
        {
            Image<Gray, byte> segmentedImage = ColorSegment(inputImage);
            Image<Gray, byte> outputImage = Erode(segmentedImage);
            VectorOfVectorOfPoint handContours = LargestContours(outputImage);

            return handContours;
        }

        /// <summary>
        /// This will find the hands and measure the distance from the top-left corner of the image.
        /// </summary>
        /// <param name="inputImage">A standard BGR image.</param>
        /// <param name="point">The distance to the hand is measured from this point. In (x, y) coordinates from the top left.</param>
        /// <returns>The distance between the top-left corner and the closest hand (in pixels).</returns>
        public static float MeasureDistance(Image<Bgr, byte> inputImage, PointF point)
        {
            VectorOfVectorOfPoint largestContours = FindHands(inputImage);
            float distance = MeasureDistance(largestContours, point);

            return distance;
        }

        public HgdData AnalyseData()
        {
            var lengthBeGaze = BeGazeData.RecordingTime.Count;
            var lengthVideo = Video.NumberOfFrames();

            if (lengthVideo != lengthBeGaze) throw new FormatException("Video and BeGaze Data not of same length.");

            throw new NotImplementedException();

            for (var i = 0; i < lengthBeGaze; i++)
            {
                VectorOfVectorOfPoint handContours = FindHands(Video.GetImageFrame());
                PointF gazeCoordinates = new PointF(BeGazeData.XGaze[i], BeGazeData.YGaze[i]);

                float distance = MeasureDistance(handContours, gazeCoordinates);
                HgdData.RawDistance.Add(distance);
            }

            return HgdData;
        }

        #endregion

        #region Private Members

        /// <summary>
        /// The two color segmentations ('Minimum' and 'HSV') are combined to give a better prediction.
        /// </summary>
        /// <param name="inputImage">A standard BGR image.</param>
        /// <returns>A grayscale image with skin being white pixels.</returns>
        private static Image<Gray, byte> ColorSegment(Image<Bgr, byte> inputImage)
        {
            var minimumSegment = MinimumSegment(inputImage);
            var hsvSegment = HsvSegment(inputImage);

            Image<Gray, byte> segmentedImage = new Image<Gray, byte>(inputImage.Size);
            CvInvoke.BitwiseAnd(minimumSegment, hsvSegment, segmentedImage);

            return segmentedImage;
        }

        /// <summary>
        /// This function compares the separte Blue, Green and Red values of each pixel and, using a clever
        /// subtraction, tries to determine if it is skin-colored. The idea is taken from this paper:
        /// "In-air gestures around unmodified mobile devices" by Song et al.
        /// </summary>
        /// <param name="inputImage">Standard BGR image.</param>
        /// <returns>Grayscale image with the white pixels containing skin.</returns>
        private static Image<Gray, byte> MinimumSegment(Image<Bgr, byte> inputImage)
        {
            Mat deltaOne = new Mat();
            Mat deltaTwo = new Mat();

            VectorOfMat bgrChannels = new VectorOfMat(3);
            CvInvoke.Split(inputImage, bgrChannels);
            CvInvoke.Subtract(bgrChannels[2], bgrChannels[1], deltaOne);
            CvInvoke.Subtract(bgrChannels[2], bgrChannels[0], deltaTwo);

            Mat mixedMat = new Mat();
            CvInvoke.Min(deltaOne, deltaTwo, mixedMat);
            Image<Gray, byte> outputImage = mixedMat.ToImage<Gray, byte>().InRange(new Gray(10), new Gray(200));

            return outputImage;
        }

        /// <summary>
        /// This function will use thresholds on the Hue value in the Hue-Saturation-Value (HSV) color space to find caucasian skin within an image.
        /// It will then return a grayscale image with the hand-containing pixels colored white.
        /// </summary>
        /// <param name="inputImage">A standard BGR image.</param>
        /// <returns>Grayscale image with white pixels containing white skin.</returns>
        private static Image<Gray, byte> HsvSegment(Image<Bgr, byte> inputImage)
        {
            var hsvImage = inputImage.Copy().Convert<Hsv, byte>();
            var outputImage = new Image<Gray, byte>(hsvImage.Size);

            var hsvThresholdOne = new Hsv(0, 0, 0);
            var hsvThresholdTwo = new Hsv(30, 255, 255);
            var hsvThresholdThree = new Hsv(160, 0, 0);
            var hsvThresholdFour = new Hsv(180, 255, 255);

            Image<Gray, byte> lowerThreshold = hsvImage.InRange(hsvThresholdOne, hsvThresholdTwo);
            Image<Gray, byte> upperThreshold = hsvImage.InRange(hsvThresholdThree, hsvThresholdFour);
            CvInvoke.BitwiseOr(lowerThreshold, upperThreshold, outputImage);

            return outputImage;
        }

        /// <summary>
        /// This function will erode a grayscale image using a standard rectangular kernel, five by five.
        /// </summary>
        /// <param name="inputImage">A standard BGR image.</param>
        /// <param name="iterations">How often the image is eroded. Standard value is 3.</param>
        /// <returns></returns>
        private static Image<Gray, byte> Erode(IImage inputImage, int iterations = 3)
        {
            Image<Gray, byte> erodedImage = new Image<Gray, byte>(inputImage.Size);

            Mat kernelMat = CvInvoke.GetStructuringElement(ElementShape.Rectangle, new Size(5, 5), new Point(-1, -1));
            CvInvoke.Erode(inputImage, erodedImage, kernelMat, new Point(-1, -1), iterations, BorderType.Default, CvInvoke.MorphologyDefaultBorderValue);

            return erodedImage;
        }

        /// <summary>
        /// This function will dilate a grayscale image using a standard rectangular kernel, five by five.
        /// </summary>
        /// <param name="inputImage">A standard BGR image.</param>
        /// <param name="iterations">How often the image is dilated. Standard value is 3.</param>
        private static Image<Gray, byte> Dilate(IImage inputImage, int iterations = 3)
        {
            Image<Gray, byte> dilatedImage = new Image<Gray, byte>(inputImage.Size);

            Mat kernelMat = CvInvoke.GetStructuringElement(ElementShape.Rectangle, new Size(5, 5), new Point(-1, -1));
            CvInvoke.Dilate(inputImage, dilatedImage, kernelMat, new Point(-1, -1), iterations, BorderType.Default, CvInvoke.MorphologyDefaultBorderValue);

            return dilatedImage;
        }

        /// <summary>
        /// This function will take a segmented grayscale image and the likeliest candidates for hands.
        /// They are chosen as the two largest contours with a size of at least 10'000 pixels.
        /// </summary>
        /// <param name="inputImage">Already segmented grayscale image.</param>
        /// <param name="pixelThreshold">Number of pixels required to be counted as a hand.</param>
        /// <param name="numberOfContours">The n largest contours which will be picked from the list.</param>
        /// <returns>Vector of contours</returns>
        public static VectorOfVectorOfPoint LargestContours(Image<Gray, byte> inputImage, int pixelThreshold = 10000, int numberOfContours = 2)
        {
            VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint();
            VectorOfVectorOfPoint sortedContours = new VectorOfVectorOfPoint();
            Mat hierarchyMat = new Mat();

            CvInvoke.FindContours(inputImage, contours, hierarchyMat, RetrType.Tree, ChainApproxMethod.ChainApproxNone);

            if (contours.Size > 0)
            {
                Dictionary<VectorOfPoint, double> contourDict = new Dictionary<VectorOfPoint, double>();
                for (int i = 0; i < contours.Size; i++)
                {
                    double contourArea = CvInvoke.ContourArea(contours[i]);
                    contourDict.Add(contours[i], contourArea);
                }

                var orderedDict = contourDict.OrderByDescending(area => area.Value).TakeWhile(area => area.Value > pixelThreshold);
                if (orderedDict.Count() > numberOfContours) orderedDict = orderedDict.Take(numberOfContours);
                foreach (var contour in orderedDict) sortedContours.Push(contour.Key);
            }

            return sortedContours;
        }

        /// <summary>
        /// This function measures the minimum distance between any point and the hand contours.
        /// Hereby, the shortest of the distances is picked as the relevant one.
        /// </summary>
        /// <param name="handContours">The hand contour[s] as vector of vector of points.</param>
        /// <param name="point">The desired [gaze] point</param>
        /// <returns>Distance in (float) number of pixels.</returns>
        private static float MeasureDistance(VectorOfVectorOfPoint handContours, PointF point)
        {
            if (handContours.Size == 0) return -1.0f;

            List<double> distances = new List<double>();
            for (int i = 0; i < handContours.Size; i++)
            {
                double distance = Math.Abs(CvInvoke.PointPolygonTest(handContours[i], point, true));
                distances.Add(distance);
            }

            return (float)distances.Min();
        }

        #endregion
    }
}