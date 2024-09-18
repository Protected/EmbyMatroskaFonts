using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Serialization;
using System;
using System.Collections.Generic;
using System.Text;

namespace EmbyMatroskaFonts
{
    public class Plugin : BasePlugin<PluginConfiguration>
    {
        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer) : base(applicationPaths, xmlSerializer)
        {
        }

        public override string Name => "MKV subtitle fonts";

        public override string Description => "Adds API endpoints for serving embedded MKV fonts to the video player";

        public override Guid Id => new Guid("D2FD5AB0-9C64-4D69-896B-C5DDD6E89AAF");
    }
}
