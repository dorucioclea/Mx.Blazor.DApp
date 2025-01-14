﻿using Mx.Blazor.DApp.Server.Services;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using HttpResponse = Mx.Blazor.DApp.Shared.Models.HttpResponse;
using Mx.Blazor.DApp.Shared.Connection;

namespace Mx.Blazor.DApp.Server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ConnectionController : ControllerBase
    {
        private readonly IConnectionService _connectionService;

        public ConnectionController(IConnectionService connectionService)
        {
            _connectionService = connectionService;
        }

        [HttpPost("verify")]
        public IActionResult Verify(ConnectionRequest request)
        {
            var response = _connectionService.Verify(request);

            if (response == null)
                return BadRequest(new HttpResponse()
                {
                    StatusCode = HttpStatusCode.BadRequest,
                    Message = "Access token could no be generated",
                    Error = "Token error"
                });

            return Ok(response);
        }
    }
}
