using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace ISTDocAi.Application.Common.BaseModels
{
    public abstract class ListQueryBase<T>
    {
        public int PageNo { get; set; }
        public int PageRowCount { get; set; }
        public string? SortField { get; set; }
        public string? SortOrder { get; set; }
    }
}
