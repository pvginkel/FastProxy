using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace FastProxy
{
    public class SimpleConnector : IConnector
    {
        private readonly IPEndPoint endpoint;
        private readonly IListener listener;

        public SimpleConnector(IPEndPoint endpoint)
            : this(endpoint, null)
        {
        }

        public SimpleConnector(IPEndPoint endpoint, IListener listener)
        {
            this.endpoint = endpoint;
            this.listener = listener;
        }

        public bool Connect(out IPEndPoint endpoint, out IListener listener)
        {
            endpoint = this.endpoint;
            listener = this.listener;

            return true;
        }
    }
}
