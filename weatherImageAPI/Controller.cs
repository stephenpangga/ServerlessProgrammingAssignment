using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using weatherImageAPI.Model;
using System;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Net;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace weatherImageAPI
{
    public class Controller
    {
        HttpClient client = new HttpClient();

        public Stream AddTextToImage(Stream imageStream, string imageName, string text, float x, float y, int fontSize, string colorHex)
        {
            var memoryStream = new MemoryStream();

            //which image.
            var image = SixLabors.ImageSharp.Image.Load(imageStream);

            Font font = SystemFonts.CreateFont("Arial", fontSize);
            image.Mutate(x => x.DrawText(text, font, Color.Black, new PointF(0, 0)));
            image.Save(@"C:\Users\Stephen Pangga\source\repos\weatherImageAPI\weatherImageAPI\TextImage\" + imageName);

            return memoryStream;
        }

        [FunctionName("GetImagesWithWeatherInfo")]
        public async Task<IActionResult> GetImagesWithWeatherInfo(
            [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req, ILogger log,
            [Queue("sisa")] IAsyncCollector<String> applicationQueue)
        {

            log.LogInformation("C# HTTP trigger function processed a request for weather json.");

            string name = req.Query["ImageStorage"];

            try 
            {
                await applicationQueue.AddAsync(name);

                return new OkObjectResult("Your images will be available soon at:");
            }
            catch(Exception e)
            {
                log.LogError(e.Message);
                return new BadRequestObjectResult(e.Message);
            }
            //return null;
        }

        [FunctionName("Queues")]
#pragma warning disable AZF0001 // Avoid async void
        public async void SendWeatherApi([QueueTrigger("sisa")] string myQueueItem, ILogger log)
#pragma warning restore AZF0001 // Avoid async void
        {
            //weather api
            string buienRadarAPI = "https://data.buienradar.nl/2.0/feed/json";
            //image api
            string imageLink = "https://picsum.photos/v2/list?page=1&limit=51";

            try
            {
                //weather api
                var apiFromBueinradar = await client.GetAsync(buienRadarAPI);
                string weatherContent = await apiFromBueinradar.Content.ReadAsStringAsync();
                string weatherData = "{\"models\":" + weatherContent + "}";

                Model.Root myDeserializedClass = JsonConvert.DeserializeObject<Model.Root>(weatherData);
                Console.WriteLine("Run the Queues");
                Console.WriteLine(myDeserializedClass.models.actual.stationmeasurements);


                //image api
                var imageApi = await client.GetAsync(imageLink);
                var imageBody = await imageApi.Content.ReadAsStringAsync();


                var image = JsonConvert.DeserializeObject<List<Model.Image>>(imageBody);

                //merge the 2 api to produce an image
                foreach (var v in image)
                {
                    using (WebClient client = new WebClient())
                    {
                        client.DownloadFile(new Uri(v.download_url), @"C:\Users\Stephen Pangga\source\repos\weatherImageAPI\weatherImageAPI\images\"+ v.id + v.author +".jpeg");
                    }

                    FileStream file = new FileStream(@"C:\Users\Stephen Pangga\source\repos\weatherImageAPI\weatherImageAPI\images\" + v.id + v.author + ".jpeg", FileMode.Open);
                    //Stream imageStream, string imageName, string text, float x, float y, int fontSize, string colorHex
                    AddTextToImage(file, $"new_{v.author}_{v.id}.jpeg", $"{myDeserializedClass.models.actual.stationmeasurements.ToString()}", 20, 20, 700, "#EDC9F4");
                    file.Close();
                }
                Console.WriteLine("done printing");

                //add the image to the blob storage
                /*StorageCredentials acc = new StorageCredentials("bobimageweatherstorage", "VHtPyVL8Rf0kkBfToE9R45yval89elUgoRTqH1X4n0pT0mvdueqfb+v0Whf2EllSxNCnd8tsJR9vqFqW+KVyxA==");
                CloudStorageAccount storage = new CloudStorageAccount(acc, useHttps: true);
                CloudBlobClient blobClient = storage.CreateCloudBlobClient();
                CloudBlobContainer Imagecontainer = blobClient.GetContainerReference("bobimageweatherstorage");
                CloudBlockBlob blockBlob = Imagecontainer.GetBlockBlobReference(mergeImage + ".png");
                blockBlob.Properties.ContentType = "image/png";
                await blockBlob.UploadFromStreamAsync(mergeImage);*/

                log.LogInformation("The images has been stored to the blob storage");
            }
            catch(Exception e)
            {
                log.LogInformation(e.Message);
            }
        }
    }
}
