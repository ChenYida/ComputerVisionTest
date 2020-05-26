using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ComputerVisionTest
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const string ComputerVisionSubscriptionKey = "a02975dd420b4895992ed0fa2c2d541e";
        private const string FaceSubscriptionKey = "026fa14df9c64b80b7cc4ec60e1b883d";

        private static readonly List<VisualFeatureTypes> Features =
            new List<VisualFeatureTypes>()
            {
                VisualFeatureTypes.Categories, VisualFeatureTypes.Description,
                VisualFeatureTypes.Faces, VisualFeatureTypes.ImageType,
                VisualFeatureTypes.Tags
            };

        private int _currentIndex;

        public MainWindow()
        {
            InitializeComponent();
        }

        public List<string> ImagePathList
        {
            get
            {
                var di = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory + "\\Images");
                return di.GetFiles().Select(t => t.FullName).ToList();
            }
        }

        private void BtnNext_OnClick(object sender, RoutedEventArgs e)
        {
            _currentIndex = _currentIndex >= ImagePathList.Count - 1 ? 0 : _currentIndex + 1;
            ImageBox.Source = new BitmapImage(new Uri(ImagePathList[_currentIndex]));
            AnalyzeImage();
            MakeAnalysisRequest();
        }

        private void BtnPrevious_OnClick(object sender, RoutedEventArgs e)
        {
            _currentIndex = _currentIndex <= 0 ? ImagePathList.Count - 1 : _currentIndex - 1;
            ImageBox.Source = new BitmapImage(new Uri(ImagePathList[_currentIndex]));
            //AnalyzeImage();
            MakeAnalysisRequest();
        }

        private async void AnalyzeImage()
        {
            Tb1.Text = string.Empty;
            var computerVision = new ComputerVisionClient(new ApiKeyServiceClientCredentials(ComputerVisionSubscriptionKey));
            computerVision.Endpoint = "https://computervisioncyd.cognitiveservices.azure.com/";
            await AnalyzeLocalAsync(computerVision, ImagePathList[_currentIndex]);
        }

        private async Task AnalyzeLocalAsync(ComputerVisionClient computerVision, string imagePath)
        {
            using (Stream imageStream = File.OpenRead(imagePath))
            {
                ImageAnalysis analysis = await computerVision.AnalyzeImageInStreamAsync(imageStream, Features);
                DisplayResults(analysis);
            }
        }

        private void DisplayResults(ImageAnalysis analysis)
        {
            if (analysis.Description.Captions.Count != 0)
            {
                Tb1.Text = analysis.Description.Captions[0].Text + "\n";
            }
            else
            {
                Console.WriteLine("No description generated.");
            }
        }

        private async void MakeAnalysisRequest()
        {
            ClearMarkersOnImage();

            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", FaceSubscriptionKey);

            string queryString = "returnFaceId=true&returnFaceLandmarks=false&returnFaceAttributes=age,gender";
            string uri = "https://facecyd.cognitiveservices.azure.com/face/v1.0/detect?" + queryString;

            string responseContent;

            byte[] byteData = GetImageAsByteArray(ImagePathList[_currentIndex]);

            using (var content = new ByteArrayContent(byteData))
            {
                content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                var response = await client.PostAsync(uri, content);
                responseContent = response.Content.ReadAsStringAsync().Result;
            }
            var jArray = (JArray)JsonConvert.DeserializeObject(responseContent);
            AddMarkersOnImage(jArray);
        }

        private void ClearMarkersOnImage()
        {
            foreach (var rectangle in CanvasRoot.Children.OfType<Rectangle>().ToList())
            {
                CanvasRoot.Children.Remove(rectangle);
            }
            foreach (var textblock in CanvasRoot.Children.OfType<TextBlock>().ToList())
            {
                CanvasRoot.Children.Remove(textblock);
            }
        }

        private static byte[] GetImageAsByteArray(string imageFilePath)
        {
            FileStream fileStream = new FileStream(imageFilePath, FileMode.Open, FileAccess.Read);
            BinaryReader binaryReader = new BinaryReader(fileStream);
            return binaryReader.ReadBytes((int)fileStream.Length);
        }

        private void AddMarkersOnImage(JArray jArray)
        {
            foreach (var obj in jArray)
            {
                var jObject = obj as JObject;
                var rectangle = new Rectangle();
                Canvas.SetLeft(rectangle, (int)jObject["faceRectangle"]["left"]);
                Canvas.SetTop(rectangle, (int)jObject["faceRectangle"]["top"]);
                rectangle.Width = (int)jObject["faceRectangle"]["width"];
                rectangle.Height = (int)jObject["faceRectangle"]["height"];
                rectangle.Stroke = new SolidColorBrush { Color = Colors.DeepSkyBlue };
                rectangle.StrokeThickness = 8;
                CanvasRoot.Children.Add(rectangle);
                var textblock = new TextBlock();
                textblock.Text = string.Format("Age:{0}\nGender:{1}", (string)jObject["faceAttributes"]["age"],
                    (string)jObject["faceAttributes"]["gender"]);
                Canvas.SetLeft(textblock, (int)jObject["faceRectangle"]["left"]);
                Canvas.SetTop(textblock, (int)jObject["faceRectangle"]["top"] + (int)jObject["faceRectangle"]["height"] + 10);
                CanvasRoot.Children.Add(textblock);
            }
        }

        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var element = sender as UIElement;
            var position = e.GetPosition(element);
            var transform = element.RenderTransform as MatrixTransform;
            var matrix = transform.Matrix;
            var scale = e.Delta >= 0 ? 1.05 : 1.0 / 1.05;
            matrix.ScaleAtPrepend(scale, scale, position.X, position.Y);
            transform.Matrix = matrix;
        }

        private Point _mouseDownPoint;

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (e.RightButton == MouseButtonState.Pressed)
            {
                var transform = ((UIElement)sender).RenderTransform as MatrixTransform;
                var mouse = transform.Inverse.Transform(e.GetPosition(this));
                var delta = Point.Subtract(mouse, _mouseDownPoint); // delta from old mouse to current mouse
                var translate = new TranslateTransform(delta.X, delta.Y);
                transform.Matrix = translate.Value * transform.Matrix;
                ((UIElement)sender).RenderTransform = transform;
                e.Handled = true;
            }
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            var transform = ((UIElement)sender).RenderTransform as MatrixTransform;
            _mouseDownPoint = transform.Inverse.Transform(e.GetPosition(this));
        }
    }
}