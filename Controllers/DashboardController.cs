using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tecomm.Models;
using Tecomm.Services;

namespace Tecomm.Controllers;

/// <summary>
/// GET /api/dashboard  – aggregated store stats  [Admin]
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
[Produces("application/json")]
public class DashboardController : ControllerBase
{
    private readonly IStatsService _stats;
    public DashboardController(IStatsService stats) => _stats = stats;

    [HttpGet]
    [ProducesResponseType(typeof(StoreStats), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult GetStats() => Ok(_stats.GetStats());
}
