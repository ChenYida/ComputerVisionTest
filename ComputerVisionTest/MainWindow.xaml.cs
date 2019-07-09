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
using System.Windows.Media.Imaging;

namespace ComputerVisionTest
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const string ComputerVisionSubscriptionKey = "a2ed2142c80e45d49f437dee30cf9a7a";
        private const string FaceSubscriptionKey = "060c030588f04f9387d17f0ce518cdf3";

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
            AnalyzeImage();
            MakeAnalysisRequest();
        }

        private async void AnalyzeImage()
        {
            var computerVision = new ComputerVisionClient(new ApiKeyServiceClientCredentials(ComputerVisionSubscriptionKey));
            computerVision.Endpoint = "https://chinaeast2.api.cognitive.azure.cn/";
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
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", FaceSubscriptionKey);

            string queryString = "returnFaceId=true&returnFaceLandmarks=false&returnFaceAttributes=age,gender";
            string uri = "https://chinaeast2.api.cognitive.azure.cn/face/v1.0/detect?" + queryString;

            HttpResponseMessage response;
            string responseContent;

            byte[] byteData = GetImageAsByteArray(ImagePathList[_currentIndex]);

            using (var content = new ByteArrayContent(byteData))
            {
                // This example uses content type "application/octet-stream".
                // The other content types you can use are "application/json" and "multipart/form-data".
                content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                response = await client.PostAsync(uri, content);
                responseContent = response.Content.ReadAsStringAsync().Result;
            }
            var jsonResponse = responseContent.Substring(1, responseContent.Length - 2);
            TbJson.Text = jsonResponse;
            var data = (JObject)JsonConvert.DeserializeObject(jsonResponse);
        }

        private static byte[] GetImageAsByteArray(string imageFilePath)
        {
            FileStream fileStream = new FileStream(imageFilePath, FileMode.Open, FileAccess.Read);
            BinaryReader binaryReader = new BinaryReader(fileStream);
            return binaryReader.ReadBytes((int)fileStream.Length);
        }
    }
}