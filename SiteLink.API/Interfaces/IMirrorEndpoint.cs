using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SiteLink.API.Interfaces
{
    public interface IMirrorEndpoint
    {
        void Send(NetworkWriter writer);
    }
}
