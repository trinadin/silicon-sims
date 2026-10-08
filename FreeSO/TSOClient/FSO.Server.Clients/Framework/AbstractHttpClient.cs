using RestSharp;
using System.Net;

namespace FSO.Server.Clients.Framework
{
    public abstract class AbstractHttpClient
    {
        public string BaseUrl { get; internal set; }
        private readonly CookieContainer Cookies = new CookieContainer();

        public AbstractHttpClient(string baseUrl)
        {
            BaseUrl = baseUrl;
        }

        public virtual void SetBaseUrl(string url)
        {
            BaseUrl = url;
        }

        protected RestClient Client()
        {
            var options = new RestClientOptions(BaseUrl)
            {
                ConfigureMessageHandler = handler =>
                {
                    if (handler is HttpClientHandler httpHandler)
                    {
                        httpHandler.CookieContainer = Cookies;
                    }

                    return handler;
                }
            };

            return new RestClient(options);
        }
    }
}
