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

        StorageCredentials credentials;
        CloudStorageAccount storageAccount;
        CloudBlobClient blobClient;
        CloudBlobContainer Imagecontainer;

        ILogger logger { get; }

        public Controller1(ILogger<Controller1> Log)
        {
            this.logger = Log;
            credentials = new StorageCredentials("weatherstorageforecast", "5KG5BxzVTu+7K+MZW7ER4rN1MLyAY0gHA/Y0QuEGeaLOBSlRMEeh/yAfiSZWPLnFzPTWa+Jj7Aq+nkBGJpSfsg==");
            storageAccount = new CloudStorageAccount(credentials, useHttps: true);
            blobClient = storageAccount.CreateCloudBlobClient();
            Imagecontainer = blobClient.GetContainerReference("images");
        }
        public async Task<MemoryStream> AddTextToImage(string imageUrl, string imageName, string text, float x, float y, int fontSize)
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
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "GetImagesWithWeatherInfo1")] HttpRequest req,
            [Queue("imagestorage")] IAsyncCollector<String> applicationQueue)
        {

            logger.LogInformation("C# HTTP trigger function processed a request for weather json.");

            string name = req.Query["imagestorage"];

            string webBase = @"https://weatherstorageforecast.blob.core.windows.net/images/";
            string links = "";
            
            for (int i = 0; i < 51; i++)
            {
                links += webBase + i + "weather?sv=2020-04-08&si=readOnly&sr=c&sig=pQ8LcLEH6GKDZBEISyLOrNlr6BWU6cXmaEzHSFtJp1E%3D \n";
            }

            try
            {
                await applicationQueue.AddAsync(name);

                return new OkObjectResult($"Your generated images with weather details will be available at: \n" + links);
                /*var content = "<html>" +
                    "<body>" +
                        "<h1>Hello World</h1>" +
                        "<p>"+ links +"<br>" +
                        "</p>" +
                    "</body>" +
                    "</html>";

                return new ContentResult()
                {
                    Content = content,
                    ContentType = "text/html",
                }; ;*/
            }
            catch (Exception e)
            {
                logger.LogError(e.Message);
                return new BadRequestObjectResult(e.Message);
            }
        }

        [FunctionName("weather-queue")]
#pragma warning disable AZF0001 // Avoid async void
        public async void mergeAPIs([QueueTrigger("imagestorage")] string content)
#pragma warning restore AZF0001 // Avoid async void
        {
            logger.LogInformation("The queue is now collecting weather information and image from API's ");
            
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

               /* //add the image to the blob storage
                StorageCredentials credentials = new StorageCredentials("weatherstorageforecast", "5KG5BxzVTu+7K+MZW7ER4rN1MLyAY0gHA/Y0QuEGeaLOBSlRMEeh/yAfiSZWPLnFzPTWa+Jj7Aq+nkBGJpSfsg==");
                CloudStorageAccount storageAccount = new CloudStorageAccount(credentials, useHttps: true);
                CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
                CloudBlobContainer Imagecontainer = blobClient.GetContainerReference("images");*/

                logger.LogInformation("Start writing weather info to image.");

                //merge the 2 api to produce an image
                for (int i = 0; i < image.Count; i++)
                {
                    string imageUrl = image[i].download_url;
                    string imageName = i + "weather"; //image[i].id + image[i].author;
                    Model.Stationmeasurement stationMeasurement = myDeserializedClass.models.actual.stationmeasurements[i];
                    string text = stationMeasurement.ToString();

                    MemoryStream mergeImage = await AddTextToImage(imageUrl,imageName,text, 0,0,100);

                    CloudBlockBlob blockBlob = Imagecontainer.GetBlockBlobReference(imageName);
                    blockBlob.Properties.ContentType = "image/png";
                    await blockBlob.UploadFromStreamAsync(mergeImage);
                    //var photoLink = blockBlob.Uri.AbsoluteUri;
                    //Console.WriteLine(photoLink);
                }

                logger.LogInformation("Done merging the weather information with the images");
                logger.LogInformation("The generated images has been stored to the blob storage");
            }
            catch (Exception e)
            {
                logger.LogInformation(e.Message);
            } 
        }

       /* public string creatUri()
        {
            SharedAccessPolicy sap = new SharedAccessPolicy
            {
                SharedAccessStartTime = now,
                SharedAccessExpiryTime = now.AddHours(1),
                Permissions = SharedAccessPermissions.Read | SharedAccessPermissions.Write | SharedAccessPermissions.Delete
            };

            string sas = blobClient.GetSharedAccessSignature(sap);

            return sas;
        }*/
    }
}
//example link
//https://weatherstorageforecast.blob.core.windows.net/images/{nameoffile}?sp=r&st=2021-09-20T11:07:07Z&se=2021-09-20T19:07:07Z&spr=https&sv=2020-08-04&sr=c&sig=%2BU5Qrnw3FsKabxOW0R95dmnwCg6gj3BB9Kfivip8Qks%3D