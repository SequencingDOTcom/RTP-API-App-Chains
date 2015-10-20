using System;
using System.IO;
using System.Net;
using Sequencing.AppChainsSample.SQAPI;

namespace Sequencing.AppChainsSample
{
    /// <summary>
    ///  Class that represents result entity if it's file	
    /// </summary>
    internal class FileResultValue : ResultValue
    {
        private readonly string name;
        private readonly string extension;
        private readonly Uri url;

        public FileResultValue(string name, string extension, Uri url) : base(ResultType.FILE)
        {
            this.name = name;
            this.extension = extension;
            this.url = url;
        }

        public string Name
        {
            get { return name; }
        }

        public Uri Url
        {
            get { return url; }
        }

        public void saveTo(string token, string fullPathWithName)
        {
            var path = Path.Combine(fullPathWithName, name);
            new SqApiWebClient(token).DownloadFile(url, path);
        }

        public string getExtension()
        {
            return extension;
        }
    }
}