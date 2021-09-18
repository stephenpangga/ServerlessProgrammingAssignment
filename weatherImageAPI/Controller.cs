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
using System.Collections.Generic;
using System.IO;
using System.Text;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Net;

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

            Font font = SystemFonts.CreateFont("Arial", 100);
            image.Mutate(x => x.DrawText(text, font, Color.Black, new PointF(0, 0)));
            image.Save(@"C:\Users\Stephen Pangga\source\repos\weatherImageAPI\weatherImageAPI\TextImage\" + imageName);

            
            return memoryStream;
        }

        [FunctionName("GetImagesWithWeatherInfo")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req, ILogger log)
        {

            log.LogInformation("C# HTTP trigger function processed a request for weather json.");

            return null;
        }

        [FunctionName("Queues")]
#pragma warning disable AZF0001 // Avoid async void
        public async void SendWeatherApi([QueueTrigger("sisa")] string myQueueItem, ILogger log)
#pragma warning restore AZF0001 // Avoid async void
        {
            //add weather api
            //string buienRadarAPI = "";
            //add image api
            string imageLink = "https://picsum.photos/v2/list?page=1&limit=51";

            try
            {
                //weather api
                var apiFromBueinradar = await client.GetAsync("https://data.buienradar.nl/2.0/feed/json");
                string weatherContent = await apiFromBueinradar.Content.ReadAsStringAsync();
                string weatherData = "{\"models\":" + weatherContent + "}";

                Model.Root myDeserializedClass = JsonConvert.DeserializeObject<Model.Root>(weatherData);
                Console.WriteLine("Run the Queues");
                //FileStream file = new FileStream("C:\\Users\\Stephen Pangga\\source\\repos\\WeatherFunctionApp\\WeatherFunctionApp\\bin\\Debug\\netcoreapp3.1\\ducklings.jpg", FileMode.Open);
                //ImageHelper.AddTextToImage(file, ($"{myDeserializedClass.models.actual.stationmeasurements[0]}", (20, 20), 20, "#EDC9F4"));
                //file.Close();
                Console.WriteLine(myDeserializedClass.models.actual.stationmeasurements);


                //image api
                var imageApi = await client.GetAsync(imageLink);
                var imageBody = await imageApi.Content.ReadAsStringAsync();

                //dynamic imagedata = JsonConvert.DeserializeObject(imageBody);
                //Console.WriteLine(imagedata);

                //Model.Image trials = JsonConvert.DeserializeObject<Model.Image>(imagedata);
                //Console.WriteLine("show me something: " + trials);

                var image = JsonConvert.DeserializeObject<List<Model.Image>>(imageBody);

                /*foreach (var item in image)
                {
                    Console.WriteLine($"{item.author} and the download is {item.download_url}");
                    //to download a file. do something like this:
                    //wb.DownloadFileAsync(new System.Uri($"{item.download_url}"), );
                }*/


                //merge 2 api to produce an image
                foreach (var v in image)
                {
                    using (WebClient client = new WebClient())
                    {
                        client.DownloadFile(new Uri(v.download_url), @"C:\Users\Stephen Pangga\source\repos\weatherImageAPI\weatherImageAPI\images\"+ v.id + v.author +".jpeg");
                    }

                    FileStream file = new FileStream(@"C:\Users\Stephen Pangga\source\repos\weatherImageAPI\weatherImageAPI\images\" + v.id + v.author + ".jpeg", FileMode.Open);
                    //Stream imageStream, string imageName, string text, float x, float y, int fontSize, string colorHex
                    AddTextToImage(file, $"new_{v.author}_{v.id}.jpeg", $"{myDeserializedClass.models.actual.stationmeasurements[0]}", 20, 20, 20, "#EDC9F4");
                    file.Close();
                }
                Console.WriteLine("done printing");

            }
            catch(Exception e)
            {
                log.LogInformation(e.Message);
            }

        }
    }
}
