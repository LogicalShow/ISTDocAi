using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ISTDocAi.Application.Common.BaseModels
{
    public record ConcurrentCommandBase
    {
        public byte[] RowVersion { get; set; }
    }
}
