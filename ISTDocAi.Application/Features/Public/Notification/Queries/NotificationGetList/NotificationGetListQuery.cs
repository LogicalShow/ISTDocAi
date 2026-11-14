using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ISTDocAi.Application.Common.BaseModels;
using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Application.Common.SharedModels;

namespace ISTDocAi.Application.Features.Public.Notification.Queries.NotificationGetList
{
    public class NotificationGetListQuery:ListQueryBase<NotificationGetListQueryResponse>,
        IQuery<GetListResult<NotificationGetListQueryResponse>>
    {
        public string? Name { get; set; }

        public string? GeneralMessageEn { get; set; }

        public string? GeneralMessageAr { get; set; }
        public bool GeneralSearch { get; set; } = false;
        public string? SearchText { get; set; }
    }
}
