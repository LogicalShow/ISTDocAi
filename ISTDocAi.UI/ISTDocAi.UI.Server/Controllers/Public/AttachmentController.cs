using ISTDocAi.UI.Server.Authorization;
using ISTDocAi.Domain.Common.Enums;
﻿using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ISTDocAi.UI.Server.Base;
using ISTDocAi.UI.Server.Services.Public.Attachment;
using ISTDocAi.UI.Server.Services.Public.Attachment.Contracts;

namespace ISTDocAi.UI.Server.Controllers.Public
{
    [Route("api/public/[controller]/[action]/{Id?}")]
    public class AttachmentController : BaseController
    {
        private readonly IAttachmentService _service;

        public AttachmentController(IAttachmentService service)
        {
            _service = service;
        }

        [HttpPost]
        [RequirePermission(PageDataEnum.ADM_ATT, ActionDataEnum.ADD)]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Add([FromForm] AttachmentAddRequest request, CancellationToken ct)
        {
            var result = await _service.AddAsync(request, ct);
            return Respond(result);
        }

        [HttpPut]
        [RequirePermission(PageDataEnum.ADM_ATT, ActionDataEnum.EDIT)]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Update([FromForm] AttachmentUpdateRequest request, CancellationToken ct)
        {
            var result = await _service.UpdateAsync(request, ct);
            return Respond(result);
        }

        [HttpDelete]
        [RequirePermission(PageDataEnum.ADM_ATT, ActionDataEnum.DELETE)]
        public async Task<IActionResult> Delete([FromBody] AttachmentDeleteRequest request, CancellationToken ct)
        {
            var result = await _service.DeleteAsync(request, ct);
            return Respond(result);
        }

        [HttpGet]
        [RequirePermission(PageDataEnum.ADM_ATT, ActionDataEnum.VIEW)]
        public async Task<IActionResult> GetById(string id, CancellationToken ct)
        {
            var result = await _service.GetByIdAsync(id, ct);
            return Respond(result);
        }

        [HttpGet]
        [RequirePermission(PageDataEnum.ADM_ATT, ActionDataEnum.VIEW)]
        public async Task<IActionResult> GetList(string referenceType, string referenceId, CancellationToken ct)
        {
            var result = await _service.GetListAsync(referenceType, referenceId, ct);
            return Respond(result);
        }

        [HttpGet]
        [RequirePermission(PageDataEnum.ADM_ATT, ActionDataEnum.VIEW)]
        public async Task<IActionResult> File(string id, bool download = false, CancellationToken ct = default)
        {
            var result = await _service.GetFileAsync(id, download, ct);

            return Respond(result, file =>
            {
                // Force inline preview OR force download
                var dispositionType = download ? "attachment" : "inline";
                var fileName = file.FileDownloadName ?? "file";

                Response.Headers["Content-Disposition"] = $"{dispositionType}; filename=\"{fileName}\"";

                return file;
            });
        }

    }
}
