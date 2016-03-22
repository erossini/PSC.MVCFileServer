using PSC.MVCFileServer.Core;
using PSC.MVCFileServer.Interfaces;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web;
using System.Web.Http;

namespace PSC.MVCFileServer.Controllers
{
    public class SimpleFilesController : ApiController
    {
        public IFileProvider FileProvider { get; set; }

        public SimpleFilesController()
        {
            FileProvider = new FileProvider();
        }

        public HttpResponseMessage Get(string fileName)
        {
            if (!FileProvider.Exists(fileName))
            {
                throw new HttpResponseException(HttpStatusCode.NotFound);
            }

            FileStream fileStream = FileProvider.Open(fileName);
            var response = new HttpResponseMessage();
            response.Content = new StreamContent(fileStream);
            response.Content.Headers.ContentDisposition
                = new ContentDispositionHeaderValue("attachment");
            response.Content.Headers.ContentDisposition.FileName = fileName;
            response.Content.Headers.ContentType
                = new MediaTypeHeaderValue("application/octet-stream");
            response.Content.Headers.ContentLength
                = FileProvider.GetLength(fileName);
            return response;
        }
    }
}