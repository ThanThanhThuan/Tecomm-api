using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tecomm.Models;
using Tecomm.Services;

namespace Tecomm.Controllers;

/// <summary>
/// GET  /api/inventory             – all inventory levels
/// GET  /api/inventory/{productId} – single item
/// PUT  /api/inventory/{productId} – update stock / threshold  [Admin only]
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class InventoryController : ControllerBase
{
    private readonly IInventoryService _inventory;

    public InventoryController(IInventoryService inventory) => _inventory = inventory;

    // GET /api/inventory
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<InventoryItem>), StatusCodes.Status200OK)]
    public IActionResult GetAll()
        => Ok(_inventory.GetAll());

    // GET /api/inventory/3
    [HttpGet("{productId:int}")]
    [ProducesResponseType(typeof(InventoryItem), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetByProductId(int productId)
    {
        var item = _inventory.GetByProductId(productId);
        return item is null
            ? NotFound(new { message = $"No inventory record for product {productId}." })
            : Ok(item);
    }

    // PUT /api/inventory/3
    [HttpPut("{productId:int}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(InventoryItem), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult Update(int productId, [FromBody] UpdateInventoryRequest request)
    {
        if (request.Quantity < 0)
            return BadRequest(new { message = "Quantity cannot be negative." });

        var updated = _inventory.Update(productId, request);
        return Ok(updated);
    }

    // GET /api/inventory/low-stock
    [HttpGet("low-stock")]
    [Authorize]
    [ProducesResponseType(typeof(IEnumerable<InventoryItem>), StatusCodes.Status200OK)]
    public IActionResult GetLowStock()
    {
        var lowStock = _inventory.GetAll()
            .Where(i => i.Quantity <= i.ReorderThreshold)
            .OrderBy(i => i.Quantity);
        return Ok(lowStock);
    }
}
