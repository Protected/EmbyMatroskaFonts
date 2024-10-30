using MediaBrowser.Model.Services;
using MediaBrowser.Controller.Net;
using MediaBrowser.Controller.Api;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Entities;
using System.IO;
using System.Diagnostics;
using System.Text;
using System.Drawing;
using System.Runtime.InteropServices;
using SkiaSharp;
using System.Reflection;

namespace EmbyMatroskaFonts
{

    [Route("/Videos/{Id}/Font/List", "GET", Summary = "Gets list of fonts used by all tracks.")]
    [Route("/Items/{Id}/Font/List", "GET", Summary = "Gets list of fonts used by all tracks.")]
    [Unauthenticated]
    public sealed class GetFontList : IReturn<List<string>>
    {
        public long Id {  get; set; }
    }

    [Route("/Videos/{Id}/Font/{Index}/Stream", "GET", Summary = "Gets font attachment.")]
    [Route("/Items/{Id}/Font/{Index}/Stream", "GET", Summary = "Gets font attachment.")]
    [Unauthenticated]
    public sealed class GetFont
    {
        [ApiMember(Name = "Id", Description = "Item Id", IsRequired = true, DataType = "long", ParameterType = "path")]
        public long Id { get; set; }

        [ApiMember(Name = "Index", Description = "The subtitle stream index", IsRequired = true, DataType = "int", ParameterType = "path")]
        public int Index { get; set; }

        public bool SetFilename { get; set; }
    }

    public class MatroskaFontService : BaseApiService
    {
        private ILogger _logger;

        private ReadOnlyMemory<byte> _defaultFont;

        public MatroskaFontService(ILogManager logManager)
        {
            _logger = logManager.GetLogger("Fonts");
            
            Stream defaultFontStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("EmbyMatroskaFonts.arialbd.ttf");
            MemoryStream ms = new MemoryStream();
            defaultFontStream.CopyTo(ms);
            _defaultFont = new ReadOnlyMemory<byte>(ms.ToArray());
        }

        protected void Log(string message)
        {
            _logger.Info(message);
        }

        public async Task<object> Get(GetFontList request)
        {
            Log("Get embedded font list " + request.Id);
            BaseItem itemById = LibraryManager.GetItemById(request.Id);

            if (!(itemById is Video))
            {
                Log("Item is not video.");
                Request.Response.StatusCode = 404;
                return null;
            }
            Video video = (Video)itemById;

            List<MediaStream> mediaStreams = video.GetMediaStreams();
            List<MediaStream> attachments = mediaStreams.FindAll(item => item.Type == MediaStreamType.Attachment);

            List<string> fontNames = new List<string>();

            for (int attIndex = 0; attIndex < attachments.Count; attIndex++)
            {
                MediaStream mediaStream = attachments[attIndex];
                ReadOnlyMemory<byte> fontContent = await GetFontFromMkv(video.Path, attIndex);
                if (fontContent.IsEmpty)
                {
                    fontNames.Add(Path.GetFileName(mediaStream.Path));
                }
                else
                {
                    using (var typeface = SKTypeface.FromData(SKData.CreateCopy(fontContent.ToArray())))
                    {
                        if (typeface != null)
                        {
                            fontNames.Add(typeface.FamilyName + Path.GetExtension(mediaStream.Path));
                        }
                        else
                        {
                            fontNames.Add(Path.GetFileName(mediaStream.Path));
                        }
                    }
                }
            }

            return ToOptimizedResult(fontNames);
        }

        public async Task<object> Get(GetFont request)
        {
            Log("Get embedded font " + request.Id + " " + request.Index);
            BaseItem itemById = LibraryManager.GetItemById(request.Id);
            IHttpResultFactory resultFactory = ResultFactory;
            Dictionary<string, string> responseHeaders = new Dictionary<string, string>((IEqualityComparer<string>)StringComparer.OrdinalIgnoreCase);

            if (!(itemById is Video))
            {
                Log("Item is not video.");
                Request.Response.StatusCode = 404;
                return null;
            }
            Video video = (Video)itemById;

            List<MediaStream> mediaStreams = video.GetMediaStreams();
            List<MediaStream> attachments = mediaStreams.FindAll(item => item.Type == MediaStreamType.Attachment);
            int attIndex = attachments.FindIndex(item => item.Index == request.Index);
            if (attIndex < 0)
            {
                Log("Index is not attachment.");
                Request.Response.StatusCode = 403;
                return null;
            }

            MediaStream font = attachments[attIndex];

            string filename = null;

            ReadOnlyMemory<byte> fontContent = await GetFontFromMkv(video.Path, attIndex);
            if (fontContent.IsEmpty)
            {
                fontContent = _defaultFont;
                if (request.SetFilename) { filename = Path.GetFileName(font.Path); }
            }
            else if (request.SetFilename)
            {
                using (var typeface = SKTypeface.FromData(SKData.CreateCopy(fontContent.ToArray())))
                {
                    if (typeface != null)
                    {
                        filename = typeface.FamilyName + Path.GetExtension(font.Path);
                    }
                    else
                    {
                        filename = Path.GetFileName(font.Path);
                    }
                }
            }

            if (filename != null)
            {
                BaseApiService.SetContentDisposition((IDictionary<string, string>)responseHeaders, filename);
            }

            StaticFileResultOptions options = new StaticFileResultOptions();
            responseHeaders["Content-Length"] = fontContent.Length.ToString();

            return resultFactory.GetResult(Request, fontContent, font.MimeType, responseHeaders);
        }

        public async Task<ReadOnlyMemory<byte>> GetFontFromMkv(string path, int index)
        {
            MemoryStream ms = new MemoryStream();

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = "-dump_attachment:t:" + index + " pipe:1 -i \"" + path + "\" -t 0 -f null null",
                RedirectStandardOutput = true,
                RedirectStandardError = false,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(path)
            };
            psi.Environment["LD_LIBRARY_PATH"] = "";

            using (Process process = new Process(){ StartInfo = psi }) {

                process.Start();

                Task outputReadTask = process.StandardOutput.BaseStream.CopyToAsync(ms);

                await outputReadTask;
                await process.WaitForExitAsync();
            }

            return new ReadOnlyMemory<byte>(ms.ToArray());
        }

    }


}
