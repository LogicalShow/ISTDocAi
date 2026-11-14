using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ISTDocAi.Application.Common.Contracts.ScheduledTask
{
    public record TaskRunResult(bool IsSuccess, string? Message = null)
    {
        public static TaskRunResult Ok(string? msg = null) => new(true, msg);
        public static TaskRunResult Fail(string? msg = null) => new(false, msg);
    }
}
