using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Auth;
using Newtonsoft.Json;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace weatherImageAPI
{
    public class Controller1
    {
        HttpClient client = new HttpClient();

        public async Task<MemoryStream> AddTextToImage(string imageUrl, string imageName, string text, float x, float y, int fontSize, string colorHex)
        {            
            HttpResponseMessage response = await client.GetAsync(imageUrl);
            Stream stream;
            MemoryStream memoryStream = new MemoryStream();

            try
            {
                byte[] byteImage = await response.Content.ReadAsByteArrayAsync();
                stream = new MemoryStream(byteImage);
            }
            catch (Exception error)
            {
                throw new Exception(error.Message);
            }

            var image = Image.Load(stream);

            Font font = SystemFonts.CreateFont("Arial", fontSize);
            image.Clone(img =>
            {
                var textGraphicOptions = new TextGraphicsOptions()
                {
                    TextOptions = {
                        WrapTextWidth = image.Width-50+1
                    }
                };
                img.DrawText(textGraphicOptions, text, font, Color.GreenYellow, new PointF(x,y));
            }).SaveAsPng(memoryStream);

            memoryStream.Position = 0;
            return memoryStream;
        }


        [FunctionName("GetImagesWithWeatherInfo1")]
        public async Task<IActionResult> GetImagesWithWeatherInfo(
            [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req, ILogger log,
            [Queue("imagestorage")] IAsyncCollector<String> applicationQueue)
        {

            log.LogInformation("C# HTTP trigger function processed a request for weather json.");

            string name = req.Query["imagestorage"];
            string webBase = @"https://weatherstorageforecast.blob.core.windows.net/images/1weather.png";
            
            try
            {
                await applicationQueue.AddAsync(name);

                return new OkObjectResult("Your generated images with weather details will be available at: " + webBase);
            }
            catch (Exception e)
            {
                log.LogError(e.Message);
                return new BadRequestObjectResult(e.Message);
            }
        }

        [FunctionName("weather-queue")]
#pragma warning disable AZF0001 // Avoid async void
        public async void mergeAPIs([QueueTrigger("imagestorage")] string content, ILogger log)
#pragma warning restore AZF0001 // Avoid async void
        {
            log.LogInformation("The queue, now collecting weather information and image from API's ");
            
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

                //image api
                var imageApi = await client.GetAsync(imageLink);
                var imageBody = await imageApi.Content.ReadAsStringAsync();
                List<Model.Image> image = JsonConvert.DeserializeObject<List<Model.Image>>(imageBody);

                //add the image to the blob storage
                StorageCredentials creds = new StorageCredentials("weatherstorageforecast", "3B2TdyesiFnuZa+W5EObCT72NfYzv/s74uYHz4nQdlVlDIdT6KAMyncLGbuhjXNEgz/14KP5w+YH6Y/ZFa60nA==");
                CloudStorageAccount storageAccount = new CloudStorageAccount(creds, useHttps: true);
                CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
                CloudBlobContainer Imagecontainer = blobClient.GetContainerReference("images");

                log.LogInformation("Start writing weather info to image.");

                //merge the 2 api to produce an image
                for (int i = 0; i < image.Count; i++)
                {
                    string imageUrl = image[i].download_url;
                    string imageName = i + "weather"; //image[i].id + image[i].author;
                    Model.Stationmeasurement stationMeasurement = myDeserializedClass.models.actual.stationmeasurements[i];
                    string text = stationMeasurement.ToString();

                    MemoryStream mergeImage = await AddTextToImage(imageUrl,imageName,text, 0,0,100, "#EDC9F4");

                    CloudBlockBlob blockBlob = Imagecontainer.GetBlockBlobReference(imageName + ".png");
                    blockBlob.Properties.ContentType = "image/png";
                    await blockBlob.UploadFromStreamAsync(mergeImage);
                    var photoLink = blockBlob.Uri.AbsoluteUri;
                    Console.WriteLine(photoLink);
                }

                log.LogInformation("Done merging the weather information with the images");
                log.LogInformation("The generated images has been stored to the blob storage");
            }
            catch (Exception e)
            {
                log.LogInformation(e.Message);
            } 
        }
    }
}
