using System;
using System.Net;

namespace Sequencing.AppChainsSample.SQAPI
{
    /// <summary>
    /// Customized web client with oauth security support
    /// </summary>
    public class SqApiWebClient : WebClient
    {
        private readonly string token;

        public SqApiWebClient(string token)
        {
            this.token = token;
        }

        protected override WebRequest GetWebRequest(Uri uri)
        {
            WebRequest w = base.GetWebRequest(uri);
            w.Headers.Add("Authorization", "Bearer "+token);
            return w;
        }
    }
}