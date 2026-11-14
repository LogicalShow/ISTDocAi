using ISTDocAi.Domain.Common.Shared.Response;
using ISTDocAi.Domain.Entities.Public;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Application.Common.SharedModels;
using ISTDocAi.Application.Features.Public.Notification.Projections;

namespace ISTDocAi.Application.Features.Public.Notification.Queries.NotificationGetList
{
    public class NotificationGetListQueryHandler(IRepositoryOp<Pub_Notification> repositoryOp) : IQueryHandler<NotificationGetListQuery, GetListResult<NotificationGetListQueryResponse>>
    {
        public async Task<Result<GetListResult<NotificationGetListQueryResponse>>> Handle(NotificationGetListQuery request, CancellationToken cancellationToken)
        {

            Expression < Func <Pub_Notification,bool>> predicate;

            if (request.GeneralSearch && !string.IsNullOrEmpty(request.SearchText))
            {
                predicate = x => (x.Name.Contains(request.SearchText) ||
                                 (x.GeneralMessageEn != null && x.GeneralMessageEn.Contains(request.SearchText)) ||
                                 (x.GeneralMessageAr != null && x.GeneralMessageAr.Contains(request.SearchText))) && x.NotificationUsers.Any(nu => nu.IsAssigned);
            }
            else
            {
                predicate = x => ((string.IsNullOrEmpty(request.Name) || x.Name.Contains(request.Name)) &&
                                 (string.IsNullOrEmpty(request.GeneralMessageEn) || (x.GeneralMessageEn != null && x.GeneralMessageEn.Contains(request.GeneralMessageEn))) &&
                                 (string.IsNullOrEmpty(request.GeneralMessageAr) || (x.GeneralMessageAr != null && x.GeneralMessageAr.Contains(request.GeneralMessageAr)))) && x.NotificationUsers.Any(nu => nu.IsAssigned);
            }

            Func<IQueryable<Pub_Notification>, IOrderedQueryable<Pub_Notification>>? orderBy = null;

            if (!string.IsNullOrEmpty(request.SortField))
            {
                switch (request.SortField.Trim().ToLowerInvariant())
                {
                    case "name":
                        orderBy = request.SortOrder == "desc"
                            ? (Func<IQueryable<Pub_Notification>, IOrderedQueryable<Pub_Notification>>)(q => q.OrderByDescending(n => n.Name))
                            : (q => q.OrderBy(n => n.Name));
                        break;
                    case "generalmessageen":
                        orderBy = request.SortOrder == "desc"
                            ? (Func<IQueryable<Pub_Notification>, IOrderedQueryable<Pub_Notification>>)(q => q.OrderByDescending(n => n.GeneralMessageEn))
                            : (q => q.OrderBy(n => n.GeneralMessageEn));
                        break;
                        case "generalmessagear":
                        orderBy = request.SortOrder == "desc"
                            ? (Func<IQueryable<Pub_Notification>, IOrderedQueryable<Pub_Notification>>)(q => q.OrderByDescending(n => n.GeneralMessageAr))
                            : (q => q.OrderBy(n => n.GeneralMessageAr));
                        break;
                    default:
                        orderBy = q => q.OrderBy(n => n.Name);
                        break;
                }
            }
            else
            {
                orderBy = q => q.OrderBy(n => n.Name);
            }

            var result = await repositoryOp.GetListAsyncIncludes(
         predicate: predicate,
                selector: NotificationGetListProjection.Selector,
        new[] { "NotificationUsers" },
         orderBy: orderBy,
        pageNo: request.PageNo,
         pageRowCount: request.PageRowCount,
         cancellationToken: cancellationToken
     );


            var mappedResult = new GetListResult<NotificationGetListQueryResponse>
            {
                Items = result.Items.Select(item => new NotificationGetListQueryResponse
                {
                    Id = item.Id,
                    Name = item.Name,
                    GeneralMessageEn = item.GeneralMessageEn,
                    GeneralMessageAr = item.GeneralMessageAr
                }),
                TotalCount = result.TotalCount
            };
            return Result.Success(mappedResult);
        }
    }
}
