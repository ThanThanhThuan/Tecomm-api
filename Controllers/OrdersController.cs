using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tecomm.Models;
using Tecomm.Services;

namespace Tecomm.Controllers;

/// <summary>
/// POST /api/orders            – place an order (JWT required – "Checkout")
/// GET  /api/orders            – all orders        [Admin]
/// GET  /api/orders/mine       – caller's orders   [Authenticated]
/// GET  /api/orders/{id}       – single order      [Authenticated]
/// PUT  /api/orders/{id}/status – update status    [Admin]
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orders;

    public OrdersController(IOrderService orders) => _orders = orders;

    // ── POST /api/orders  (Checkout – JWT required) ───────────────────────────
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(Order), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Create([FromBody] CreateOrderRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var email  = User.FindFirstValue(ClaimTypes.Email)!;

        var (order, error) = _orders.Create(userId, email, request);

        if (error is not null)
            return BadRequest(new { message = error });

        return CreatedAtAction(nameof(GetById), new { id = order!.Id }, order);
    }

    // ── GET /api/orders  (Admin: see all) ────────────────────────────────────
    [HttpGet]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(IEnumerable<Order>), StatusCodes.Status200OK)]
    public IActionResult GetAll()
        => Ok(_orders.GetAll());

    // ── GET /api/orders/mine ──────────────────────────────────────────────────
    [HttpGet("mine")]
    [Authorize]
    [ProducesResponseType(typeof(IEnumerable<Order>), StatusCodes.Status200OK)]
    public IActionResult GetMine()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return Ok(_orders.GetByUser(userId));
    }

    // ── GET /api/orders/{id} ──────────────────────────────────────────────────
    [HttpGet("{id:int}")]
    [Authorize]
    [ProducesResponseType(typeof(Order), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetById(int id)
    {
        var order = _orders.GetById(id);
        if (order is null)
            return NotFound(new { message = $"Order {id} not found." });

        // Non-admins can only see their own orders
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var isAdmin = User.IsInRole("Admin");
        if (!isAdmin && order.UserId != userId)
            return Forbid();

        return Ok(order);
    }

    // ── PUT /api/orders/{id}/status  [Admin] ──────────────────────────────────
    [HttpPut("{id:int}/status")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult UpdateStatus(int id, [FromBody] UpdateOrderStatusRequest request)
    {
        var updated = _orders.UpdateStatus(id, request.Status);
        return updated
            ? Ok(new { message = $"Order {id} status updated to {request.Status}." })
            : NotFound(new { message = $"Order {id} not found." });
    }
}

public class UpdateOrderStatusRequest
{
    public OrderStatus Status { get; set; }
}
