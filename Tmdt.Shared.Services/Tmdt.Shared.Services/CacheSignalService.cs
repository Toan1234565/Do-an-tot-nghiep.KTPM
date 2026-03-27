using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tmdt.Shared.Services
{
    public class CacheSignalService
    {
        public CancellationTokenSource TokenSource { get; set; } = new CancellationTokenSource();
        public void Reset()
        {
            TokenSource.Cancel();
            TokenSource = new CancellationTokenSource();
        }
    }
}
