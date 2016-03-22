using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace PSC.MVCFileServer.Core
{
    public class ContentInfo
    {
        public long From;
        public long To;
        public bool IsPartial;
        public long Length;
    }
}