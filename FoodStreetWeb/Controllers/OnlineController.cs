using Microsoft.AspNetCore.Mvc;
using FoodStreetWeb.Services;

namespace FoodStreetWeb.Controllers;

[Route("api/[controller]")]
[ApiController]
public class OnlineController : ControllerBase
{
    private readonly OnlineUsersService _onlineService;

    public OnlineController(OnlineUsersService onlineService)
    {
        _onlineService = onlineService;
    }

    [HttpGet("count")]
    public IActionResult GetOnlineCount()
    {
        var count = _onlineService.GetOnlineCount();
        return Ok(new { online = count });
    }
}