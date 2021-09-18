using System;
using System.Collections.Generic;
using System.Text;

namespace weatherImageAPI.Model
{
    public class Image
    {
        public string id { get; set; }
        public string author { get; set; }
        public int width { get; set; }
        public int height { get; set; }
        public string url { get; set; }
        public string download_url { get; set; }
    }
}
