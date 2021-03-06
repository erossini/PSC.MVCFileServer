﻿using PSC.MVCFileServer.Core;
using PSC.MVCFileServer.Interfaces;
using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web.Http;

namespace PSC.MVCFileServer.Controllers
{
    public class MemMappedFilesController : ApiController
    {
        private const string MapNamePrefix = "FileServerMap";

        public IFileProvider FileProvider { get; set; }

        public MemMappedFilesController()
        {
            FileProvider = new FileProvider();
        }

        private ContentInfo GetContentInfoFromRequest(HttpRequestMessage request, long entityLength)
        {
            var result = new ContentInfo
            {
                From = 0,
                To = entityLength - 1,
                IsPartial = false,
                Length = entityLength
            };
            RangeHeaderValue rangeHeader = request.Headers.Range;
            if (rangeHeader != null && rangeHeader.Ranges.Count != 0)
            {
                //we support only one range
                if (rangeHeader.Ranges.Count > 1)
                {
                    //we probably return other status code here
                    throw new HttpResponseException(HttpStatusCode.RequestedRangeNotSatisfiable);
                }
                RangeItemHeaderValue range = rangeHeader.Ranges.First();
                if (range.From.HasValue && range.From < 0 || range.To.HasValue && range.To > entityLength - 1)
                {
                    throw new HttpResponseException(HttpStatusCode.RequestedRangeNotSatisfiable);
                }

                result.From = range.From ?? 0;
                result.To = range.To ?? entityLength - 1;
                result.IsPartial = true;
                result.Length = entityLength;
                if (range.From.HasValue && range.To.HasValue)
                {
                    result.Length = range.To.Value - range.From.Value + 1;
                }
                else if (range.From.HasValue)
                {
                    result.Length = entityLength - range.From.Value + 1;
                }
                else if (range.To.HasValue)
                {
                    result.Length = range.To.Value + 1;
                }
            }

            return result;
        }

        private void SetResponseHeaders(HttpResponseMessage response, ContentInfo contentInfo,
            long fileLength, string fileName)
        {
            response.Headers.AcceptRanges.Add("bytes");
            response.StatusCode = contentInfo.IsPartial ? HttpStatusCode.PartialContent
                : HttpStatusCode.OK;
            response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment");
            response.Content.Headers.ContentDisposition.FileName = fileName;
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            response.Content.Headers.ContentLength = contentInfo.Length;
            if (contentInfo.IsPartial)
            {
                response.Content.Headers.ContentRange
                    = new ContentRangeHeaderValue(contentInfo.From, contentInfo.To, fileLength);
            }
        }


        public HttpResponseMessage Head(string fileName)
        {
            //string fileName = GetFileName(name);
            if (!FileProvider.Exists(fileName))
            {
                //if file does not exist return 404
                throw new HttpResponseException(HttpStatusCode.NotFound);
            }
            long fileLength = FileProvider.GetLength(fileName);
            ContentInfo contentInfo = GetContentInfoFromRequest(this.Request, fileLength);

            var response = new HttpResponseMessage();
            response.Content = new ByteArrayContent(new byte[0]);
            SetResponseHeaders(response, contentInfo, fileLength, fileName);
            return response;
        }

        public HttpResponseMessage Get(string fileName)
        {
            if (!FileProvider.Exists(fileName))
            {
                //if file does not exist return 404
                throw new HttpResponseException(HttpStatusCode.NotFound);
            }
            long fileLength = FileProvider.GetLength(fileName);
            ContentInfo contentInfo = GetContentInfoFromRequest(this.Request, fileLength);
            string mapName = GenerateMapNameFromName(fileName);


            MemoryMappedFile mmf = null;
            try
            {
                mmf = MemoryMappedFile.OpenExisting(mapName, MemoryMappedFileRights.Read);
            }
            catch (FileNotFoundException)
            {
                //every time we use an exception to control flow a kitten dies

                mmf = MemoryMappedFile.CreateFromFile(FileProvider.Open(fileName), mapName, fileLength,
                                                      MemoryMappedFileAccess.Read, null, HandleInheritability.None,
                                                      false);
            }
            using (mmf)
            {
                Stream stream
                    = contentInfo.IsPartial
                    ? mmf.CreateViewStream(contentInfo.From, contentInfo.Length, MemoryMappedFileAccess.Read)
                    : mmf.CreateViewStream(0, fileLength, MemoryMappedFileAccess.Read);

                var response = new HttpResponseMessage();
                response.Content = new StreamContent(stream);
                SetResponseHeaders(response, contentInfo, fileLength, fileName);
                return response;
            }
        }

        private string GenerateMapNameFromName(string fileName)
        {
            return String.Format("{0}_{1}", MapNamePrefix, fileName);
        }
    }
}