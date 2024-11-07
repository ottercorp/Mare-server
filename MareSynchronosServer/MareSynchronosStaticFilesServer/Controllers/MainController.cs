﻿using MareSynchronos.API.Routes;
using MareSynchronosStaticFilesServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MareSynchronosStaticFilesServer.Controllers;

[Route(MareFiles.Main)]
public class MainController : ControllerBase
{
    private readonly IClientReadyMessageService _messageService;

    public MainController(ILogger<MainController> logger, IClientReadyMessageService mareHub) : base(logger)
    {
        _messageService = mareHub;
    }

    [HttpGet(MareFiles.Main_SendReady)]
    [Authorize(Policy = "Internal")]
    public async Task<IActionResult> SendReadyToClients(string uid, Guid requestId)
    {
        await _messageService.SendDownloadReady(uid, requestId).ConfigureAwait(false);
        return Ok();
    }
}