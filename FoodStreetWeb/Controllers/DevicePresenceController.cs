using FoodStreetWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace FoodStreetWeb.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DevicePresenceController : ControllerBase
    {
        private readonly OnlineDeviceStore _onlineDeviceStore;

        public DevicePresenceController(OnlineDeviceStore onlineDeviceStore)
        {
            _onlineDeviceStore = onlineDeviceStore;
        }

        [HttpGet("online-devices")]
        public IActionResult GetOnlineDevices()
        {
            var devices = _onlineDeviceStore.GetOnlineDevices();
            return Ok(new { count = devices.Count, devices });
        }

        [HttpPost("heartbeat")]
        public IActionResult Heartbeat([FromBody] DeviceHeartbeatRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.DeviceId))
                return BadRequest("DeviceId không hợp lệ");

            _onlineDeviceStore.TouchHeartbeat(request.DeviceId.Trim());
            return Ok(new { ok = true });
        }

        [HttpGet("is-online/{deviceId}")]
        public IActionResult IsOnline(string deviceId)
        {
            return Ok(new { deviceId, online = _onlineDeviceStore.IsOnline(deviceId) });
        }

        public class DeviceHeartbeatRequest
        {
            public string DeviceId { get; set; } = string.Empty;
        }
    }
}
