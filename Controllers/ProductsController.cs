using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tecomm.Models;
using Tecomm.Services;

namespace Tecomm.Controllers;

/// <summary>
/// GET    /api/products              – list (with ?category= / ?search= filters)
/// GET    /api/products/categories   – distinct categories
/// GET    /api/products/{id}         – single product
/// POST   /api/products              – create product  [Admin]
/// DELETE /api/products/{id}         – remove product  [Admin]
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _products;

    public ProductsController(IProductService products) => _products = products;

    // GET /api/products
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<Product>), StatusCodes.Status200OK)]
    public IActionResult GetAll(
        [FromQuery] string? category = null,
        [FromQuery] string? search   = null)
        => Ok(_products.GetAll(category, search));

    // GET /api/products/categories
    [HttpGet("categories")]
    [ProducesResponseType(typeof(IEnumerable<string>), StatusCodes.Status200OK)]
    public IActionResult GetCategories()
        => Ok(_products.GetAll().Select(p => p.Category).Distinct().OrderBy(c => c));

    // GET /api/products/5
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(Product), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetById(int id)
    {
        var p = _products.GetById(id);
        return p is null ? NotFound(new { message = $"Product {id} not found." }) : Ok(p);
    }

    // POST /api/products  [Admin]
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(Product), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult Create([FromBody] CreateProductRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Product name is required." });
        if (request.Price <= 0)
            return BadRequest(new { message = "Price must be greater than zero." });
        if (string.IsNullOrWhiteSpace(request.Category))
            return BadRequest(new { message = "Category is required." });

        var product = _products.Create(request);
        return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
    }

    // DELETE /api/products/5  [Admin]
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Delete(int id)
    {
        var deleted = _products.Delete(id);
        return deleted ? NoContent() : NotFound(new { message = $"Product {id} not found." });
    }
}
