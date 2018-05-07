﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using WpfHandGazeDistance.Helpers;
using WpfHandGazeDistance.Models;
using WpfHandGazeDistance.ViewModels.Base;

namespace WpfHandGazeDistance.ViewModels
{
    public class HgdViewModel : BaseViewModel
    {
        #region Private Properties

        private const float Fps = 60;

        private int _longActionCount;

        private int _stdDevPeriod;

        private int _bufferLength;

        private int _medianPeriod;

        private int _stdDevThreshold;

        private HandDetector _handDetector;

        private VideoEditor _videoEditor;

        private int _progress;

        private BitmapSource _thumbnail;

        private string _shortVideoPath = "Select";

        private string _shortBeGazePath = "Select";

        private string _shortFolderPath = "Select";

        #endregion

        #region Public Properties

        public ObservableCollection<Parameter> ParameterList;

        public BitmapSource Thumbnail
        {
            get => _thumbnail;
            set => ChangeAndNotify(value, ref _thumbnail);
        }

        public Video Video { get; set; }

        public HgdData HgdData;

        public BeGazeData BeGazeData;

        public string VideoPath;

        public string BeGazePath;

        public string FolderPath;

        public string OutputPath;

        public string ShortVideoPath
        {
            get => _shortVideoPath;
            set => ChangeAndNotify(value, ref _shortVideoPath);
        }

        public string ShortBeGazePath
        {
            get => _shortBeGazePath;
            set => ChangeAndNotify(value, ref _shortBeGazePath);
        }

        public string ShortFolderPath
        {
            get => _shortFolderPath;
            set => ChangeAndNotify(value, ref _shortFolderPath);
        }

        public int Progress
        {
            get => _progress;
            set => ChangeAndNotify(value, ref _progress);
        }

        #endregion

        #region Constructors

        public HgdViewModel(ObservableCollection<Parameter> parameterList)
        {
            HgdData = new HgdData();

            ParameterList = parameterList;
            _longActionCount = (int)(ParameterList[0].Value * Fps);
            _stdDevPeriod = (int)(ParameterList[1].Value * Fps);
            _bufferLength = (int)(ParameterList[2].Value * Fps);
            _medianPeriod = (int)ParameterList[3].Value;
            _stdDevThreshold = (int)ParameterList[4].Value;
        }

        public HgdViewModel(BeGazeData beGazeData, Video video, ObservableCollection<Parameter> parameterList)
        {
            HgdData = new HgdData();

            ParameterList = parameterList;
            _longActionCount = (int) (ParameterList[0].Value * Fps);
            _stdDevPeriod = (int) (ParameterList[1].Value * Fps);
            _bufferLength = (int) (ParameterList[2].Value * Fps);
            _medianPeriod = (int) ParameterList[3].Value;
            _stdDevThreshold = (int) ParameterList[4].Value;

            _handDetector = new HandDetector(beGazeData, video);
        }

        #endregion

        #region Commands

        public ICommand AnalyseCommand => new RelayCommand(AnalyseData, true);

        public ICommand MedianCommand => new RelayCommand(HgdMedian, true);

        public ICommand LongActionsCommand => new RelayCommand(HgdLongActions, true);

        public ICommand StdDevCommand => new RelayCommand(HgdStdDev, true);

        public ICommand ThresholdCommand => new RelayCommand(HgdThreshold, true);

        public ICommand UsabilityIssuesCommand => new RelayCommand(HgdUsability, true);

        //public ICommand LoadHgdCommand => new RelayCommand(LoadHgd, true);

        public ICommand SaveHgdCommand => new RelayCommand(SaveHgd, true);

        public ICommand LoadVideoCommand => new RelayCommand(LoadVideo, true);

        public ICommand LoadBeGazeCommand => new RelayCommand(LoadBeGaze, true);

        public ICommand SetFolderPathCommand => new RelayCommand(SetFolderPath, true);

        #endregion

        #region Public Members

        public void LoadHgd(string loadPath)
        {
            HgdData = new HgdData();
            if (loadPath != null)
            {
                HgdData.LoadData(loadPath);
                HgdData.RecordingTime = RecordingTimeFromConstant(HgdData.RecordingTime.Count);
            }
        }

        public void SaveHgd()
        {
            string savePath = FileManager.SaveFileDialog();
            if (savePath != null) HgdData.SaveData(savePath);
        }

        public void SaveHgd(string savePath)
        {
            HgdData.SaveData(savePath);
        }

        public void AnalyseData()
        {
            _handDetector = new HandDetector(BeGazeData, Video);
            HgdData = _handDetector.MeasureRawHgd();
            HgdData.RecordingTime = RecordingTimeFromVideo();
            HgdData.RawDistance = PruneValues(HgdData.RawDistance);
            HgdData.MedianDistance = MovingMedian(HgdData.RawDistance, _medianPeriod);
            HgdData.LongActions = LowPass(HgdData.MedianDistance, _longActionCount);
            HgdData.StandardDeviation = MovingStdDev(HgdData.LongActions, _stdDevPeriod);
            HgdData.RigidActions = Threshold(HgdData.StandardDeviation, _stdDevThreshold);
            HgdData.UsabilityIssues = ConvertToBinary(HgdData.RigidActions);
            HgdData.BufferedUsabilityIssues = Buffer(HgdData.UsabilityIssues, _stdDevPeriod, _bufferLength);

            SaveHgd(OutputPath);
            _videoEditor = new VideoEditor(VideoPath);
            _videoEditor.CutSnippets(FolderPath, HgdData);
        }

        #endregion

        #region Private Members

        private void LoadVideo()
        {
            VideoPath = FileManager.OpenFileDialog(".avi");
            if (VideoPath != null)
            {
                Video = new Video(VideoPath);
                //Video.ThumbnailImage.Resize(0.25);
                Thumbnail = Video.ThumbnailImage.BitMapImage;

                ShortVideoPath = ShortenPath(VideoPath);
            }
        }

        private void LoadBeGaze()
        {
            BeGazePath = FileManager.OpenFileDialog(".txt");
            if (BeGazePath != null)
            {
                BeGazeData = new BeGazeData(BeGazePath);

                ShortBeGazePath = ShortenPath(BeGazePath);
            }
        }

        private void SetFolderPath()
        {
            FolderPath = FileManager.SelectFolderDialog() + @"\";
            if (FolderPath != null)
            {
                ShortFolderPath = ShortenPath(FolderPath);

                string fileName = Path.GetFileNameWithoutExtension(VideoPath.Substring(VideoPath.LastIndexOf("\\") + 1));
                OutputPath = FolderPath + fileName + ".csv";
            }
        }

        private void HgdMedian()
        {
            HgdData.RawDistance = PruneValues(HgdData.RawDistance);
            HgdData.MedianDistance = MovingMedian(HgdData.RawDistance, _medianPeriod);
        }

        private void HgdLongActions()
        {
            HgdData.LongActions = LowPass(HgdData.MedianDistance, _longActionCount);
        }

        private void HgdStdDev()
        {
            HgdData.StandardDeviation = MovingStdDev(HgdData.LongActions, _stdDevPeriod);
        }

        private void HgdThreshold()
        {
            HgdData.RigidActions = Threshold(HgdData.StandardDeviation, _stdDevThreshold);
        }

        private void HgdUsability()
        {
            HgdData.UsabilityIssues = ConvertToBinary(HgdData.RigidActions);
            HgdData.BufferedUsabilityIssues = Buffer(HgdData.UsabilityIssues, _stdDevPeriod, _bufferLength);
        }

        private string ShortenPath(string inputPath)
        {
            string fileName = inputPath.Substring(inputPath.LastIndexOf("\\") + 1);
            inputPath = inputPath.Remove(inputPath.LastIndexOf("\\"));
            string parentFolder = inputPath.Substring(inputPath.LastIndexOf("\\") + 1);

            return @"...\" + parentFolder + @"\" + fileName;
        }

        #region HGD Manipulation

        private List<float> RecordingTimeFromVideo()
        {
            List<float> recordingTime = new List<float>();
            
            if (_handDetector != null)
            {
                float frameStep = 1000 / _handDetector.Video.Fps;
                for (int i = 0; i < _handDetector.Video.FrameCount; i++)
                {
                    recordingTime.Add(frameStep * i);
                }
            }
            
            return recordingTime;
        }

        private List<float> RecordingTimeFromConstant(int length)
        {
            List<float> recordingTime = new List<float>();

            float frameStep = 1000 / Fps;
            for (int i = 0; i < length; i++)
            {
                recordingTime.Add(frameStep * i);
            }

            return recordingTime;
        }

        private static List<float> PruneValues(List<float> inputValues)
        {
            List<float> outputValues = new List<float>();

            for (int i = 0; i < inputValues.Count; i++)
            {
                outputValues.Add(Math.Abs(inputValues[i] - (-1f)) < 0.0001f ? float.NaN : inputValues[i]);
            }

            return outputValues;
        }

        /// <summary>
        /// This function will take a list of floats and apply a moving median on it. 
        /// Within a window of length 'period' the values are replaced by the median value within that window.
        /// The new list of floats is then returned.
        /// 
        /// The median is calculated by taking the middle value (for odd number of values) or the two neighboring
        /// values and taking the average of the two (i.e. 10 values = (5th value + 6th value) / 2).
        /// </summary>
        /// <param name="inputValues">A list of float values which should be filtered.</param>
        /// <param name="period">The size of the rolling median window.</param>
        /// <returns></returns>
        private static List<float> MovingMedian(List<float> inputValues, int period)
        {
            List<float> outputValues = new List<float>();

            for (int i = 0; i < inputValues.Count; i++)
            {
                if (i < period - 1)
                    outputValues.Add(Single.NaN);
                else
                {
                    var values = new List<float>();
                    for (int x = i; x > i - period; x--)
                        values.Add(inputValues[x]);
                    values.Sort();

                    float median;
                    if (period % 2 == 0)
                        median = (values[period / 2] + values[period / 2 - 1]) / 2;
                    else
                        median = values[(int)(period / 2 + 0.5)];
                    outputValues.Add(median);
                }
            }

            return outputValues;
        }

        /// <summary>
        /// This function will filter out short 'actions', intervals of HGD which are shorter than the
        /// minimum value. A temporary list is created and values are appended until a NaN value is hit.
        /// The length of the temporary list is then checked and added to the main list if its long enough.
        /// </summary>
        /// <param name="inputValues">A list of input float values (MedianDistance).</param>
        /// <param name="longActionsCount">The minimum amount of values required to count (e.g. 120)</param>
        /// <returns>A list of float with only the long actions.</returns>
        private static List<float> LowPass(List<float> inputValues, int longActionsCount)
        {
            List<float> outputValues = new List<float>();

            for (int i = 0; i < inputValues.Count; i++)
            {
                if (Single.IsNaN(inputValues[i]))
                {
                    outputValues.Add(Single.NaN);
                    continue;
                }

                List<float> currentWindow = ValueWindow(inputValues, i);

                if (currentWindow.Count > longActionsCount)
                {
                    outputValues.AddRange(currentWindow);
                }
                else
                {
                    List<float> nanList = Enumerable.Range(0, currentWindow.Count).Select(n => Single.NaN).ToList();
                    outputValues.AddRange(nanList);
                }

                i += currentWindow.Count - 1;
            }

            return outputValues;
        }

        /// <summary>
        /// This function will compute the standard deviation of a set of input values within a moving window.
        /// The window is advanced along the data set if enough values are available, the standard deviation
        /// is written at the beginning of said window.
        /// </summary>
        /// <param name="inputValues">A set of float values (e.g. LongActions).</param>
        /// <param name="period">The size of the moving window (e.g. 120).</param>
        /// <returns>Standard deviation in a list of float.</returns>
        private static List<float> MovingStdDev(List<float> inputValues, int period)
        {
            List<float> outputValues = new List<float>();

            for (int i = 0; i < inputValues.Count - period; i++)
            {
                if (Single.IsNaN(inputValues[i]))
                {
                    outputValues.Add(Single.NaN);
                    continue;
                }

                List<float> currentWindow = new List<float>();
                for (int j = 0; j < period; j++)
                {
                    currentWindow.Add(inputValues[i + j]);
                }
                outputValues.Add(CalculateStdDev(currentWindow));
            }
            outputValues.AddRange(Enumerable.Range(0, period).Select(n => Single.NaN).ToList());

            return outputValues;
        }

        /// <summary>
        /// This function will apply a upper threshold to all values within a list of floats. Any value
        /// above the threshold is replace with NaN.
        /// </summary>
        /// <param name="inputValues">List of floats (e.g. StandardDeviation)</param>
        /// <param name="threshold">The upper threshold (e.g. 60px)</param>
        /// <returns>A list of floats.</returns>
        private static List<float> Threshold(List<float> inputValues, float threshold)
        {
            List<float> outputValues = new List<float>();

            for (int i = 0; i < inputValues.Count; i++)
            {
                if (inputValues[i] > threshold)
                {
                    outputValues.Add(Single.NaN);
                }
                else
                {
                    outputValues.Add(inputValues[i]);
                }
            }

            return outputValues;
        }

        /// <summary>
        /// This will replace any values into 1 and keep the NaN for the rest.
        /// </summary>
        /// <param name="inputValues">List of floats (e.g. RigidActions</param>
        /// <returns>List of floats</returns>
        private static List<float> ConvertToBinary(List<float> inputValues)
        {
            List<float> outputValues = new List<float>();

            foreach (var value in inputValues)
            {
                outputValues.Add(Single.IsNaN(value) ? Single.NaN : 1);
            }

            return outputValues;
        }

        /// <summary>
        /// This will make the length of the standard deviation data longer again, so that it
        /// more closely matches the actual duration of the original action. This means adding
        /// a window with the same length as the rolling window of the standard deviation. In addition,
        /// a buffer is added before and after each action (typically 0.5s each). In both cases, 
        /// the average value of the data within that action is added in the buffers.
        /// </summary>
        /// <param name="inputValues">List of floats (e.g. RigidActions)</param>
        /// <param name="windowLength">The length of the standard deviation rolling window, 
        /// used back in MovingStdDev().</param>
        /// <param name="bufferLength">The length of the buffer window.</param>
        /// <returns></returns>
        private static List<float> Buffer(List<float> inputValues, int windowLength, int bufferLength)
        {
            List<float> outputValues = new List<float>();

            int count = 0;
            for (int i = 0; i < inputValues.Count; i++)
            {
                if (Single.IsNaN(inputValues[i]))
                {
                    outputValues.Add(Single.NaN);
                    continue;
                }

                List<float> currentWindow = ValueWindow(inputValues, i);
                List<float> bufferList = Enumerable.Repeat(currentWindow.Average(), bufferLength).ToList();
                List<float> stdDevWindow = Enumerable.Repeat(currentWindow.Average(), windowLength).ToList();
                count++;

                // The first action is at the very start of the video and the output is not
                // far enough to contain more values than bufferLength
                if (outputValues.Count > bufferLength)
                {
                    outputValues.RemoveRange(outputValues.Count - bufferLength, bufferLength);
                    outputValues.AddRange(bufferList);
                }

                outputValues.AddRange(currentWindow);
                i += currentWindow.Count - 1;

                if (inputValues.Count > outputValues.Count + windowLength + bufferLength)
                {
                    outputValues.AddRange(stdDevWindow);
                    outputValues.AddRange(bufferList);
                    i += windowLength + bufferLength;
                }
                else
                {
                    if (inputValues.Count > outputValues.Count + windowLength)
                    {
                        outputValues.AddRange(stdDevWindow);
                    }
                    List<float> fillerList = Enumerable.Repeat(currentWindow.Average(), inputValues.Count - outputValues.Count).ToList();
                    outputValues.AddRange(fillerList);
                    break;
                }
            }

            return outputValues;
        }

        /// <summary>
        /// Calculates the standard deviation of a list of floats.
        /// </summary>
        /// <param name="inputValues">A list of float values.</param>
        /// <returns>Standard deviation as a float.</returns>
        private static float CalculateStdDev(List<float> inputValues)
        {
            float variance = 0f;
            float average = inputValues.Sum() / inputValues.Count;

            for (int i = 0; i < inputValues.Count; i++)
            {
                variance += (float)Math.Pow(inputValues[i] - average, 2);
            }

            variance /= inputValues.Count - 1;

            return (float)Math.Sqrt(variance);
        }

        private static List<float> ValueWindow(List<float> inputValues, int index)
        {
            List<float> currentWindow = new List<float>();
            while (!Single.IsNaN(inputValues[index]))
            {
                currentWindow.Add(inputValues[index]);
                if (index + 1 < inputValues.Count)
                {
                    if (Single.IsNaN(inputValues[index + 1])) break;
                    index++;
                }
                else
                {
                    break;
                }
            }

            return currentWindow;
        }

        #endregion

        #endregion
    }
}
