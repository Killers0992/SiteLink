using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SiteLink.API.Networking.Common
{
    public class SyncedNetworkProperty
    {
        public virtual void OnSerializeAll(NetworkWriter writer) { }
        public virtual void OnSerializeDelta(NetworkWriter writer) { }
        public virtual void OnDeserializeAll(NetworkReader reader) { }
        public virtual void OnDeserializeDelta(NetworkReader reader) { }
    }
}
