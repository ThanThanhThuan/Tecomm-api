using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Tecomm.Data;
using Tecomm.Models;

namespace Tecomm.Services;

// ═══════════════════════════════════════════════════════════════════════════════
//  INTERFACES  (registered in DI container)
// ═══════════════════════════════════════════════════════════════════════════════

public interface IProductService
{
    IEnumerable<Product> GetAll(string? category = null, string? search = null);
    Product? GetById(int id);
    Product Create(CreateProductRequest request);
    bool Delete(int id);
}

public interface IInventoryService
{
    IEnumerable<InventoryItem> GetAll();
    InventoryItem? GetByProductId(int productId);
    InventoryItem Update(int productId, UpdateInventoryRequest request);
    bool Deduct(int productId, int quantity);
}

public interface IOrderService
{
    IEnumerable<Order> GetAll();
    IEnumerable<Order> GetByUser(string userId);
    Order? GetById(int id);
    (Order? order, string? error) Create(string userId, string email, CreateOrderRequest request);
    bool UpdateStatus(int orderId, OrderStatus status);
}

public interface IAuthService
{
    (AppUser? user, string? error) Register(RegisterRequest request);
    (AuthResponse? response, string? error) Login(LoginRequest request);
}

public interface IStatsService
{
    StoreStats GetStats();
}

// ═══════════════════════════════════════════════════════════════════════════════
//  IMPLEMENTATIONS
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>Product catalogue – CRUD operations.</summary>
public class ProductService : IProductService
{
    private readonly InMemoryStore _store;
    public ProductService(InMemoryStore store) => _store = store;

    public IEnumerable<Product> GetAll(string? category = null, string? search = null)
    {
        var products = _store.GetProducts().AsEnumerable();
        if (!string.IsNullOrWhiteSpace(category))
            products = products.Where(p => p.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(search))
            products = products.Where(p =>
                p.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                p.Description.Contains(search, StringComparison.OrdinalIgnoreCase));
        return products;
    }

    public Product? GetById(int id) => _store.GetProduct(id);

    public Product Create(CreateProductRequest req)
    {
        var product = _store.AddProduct(new Product
        {
            Name        = req.Name.Trim(),
            Description = req.Description.Trim(),
            Price       = req.Price,
            Category    = req.Category.Trim(),
            ImageUrl    = string.IsNullOrWhiteSpace(req.ImageUrl)
                ? $"https://placehold.co/300x200/1a1a2e/white?text={Uri.EscapeDataString(req.Name)}"
                : req.ImageUrl
        });
        // Seed inventory for new product
        _store.UpdateInventory(product.Id, req.InitialStock, req.ReorderThreshold);
        return product;
    }

    public bool Delete(int id) => _store.DeleteProduct(id);
}

/// <summary>Inventory management – update stock levels and thresholds.</summary>
public class InventoryService : IInventoryService
{
    private readonly InMemoryStore _store;
    public InventoryService(InMemoryStore store) => _store = store;

    public IEnumerable<InventoryItem> GetAll() => _store.GetInventory().Values;
    public InventoryItem? GetByProductId(int productId) => _store.GetInventoryItem(productId);

    public InventoryItem Update(int productId, UpdateInventoryRequest request)
        => _store.UpdateInventory(productId, request.Quantity, request.ReorderThreshold);

    public bool Deduct(int productId, int quantity)
        => _store.DeductInventory(productId, quantity);
}

/// <summary>Order processing – validates stock, deducts inventory, persists orders.</summary>
public class OrderService : IOrderService
{
    private readonly InMemoryStore _store;
    private readonly IProductService _productSvc;
    private readonly IInventoryService _inventorySvc;

    public OrderService(InMemoryStore store, IProductService productSvc, IInventoryService inventorySvc)
    {
        _store        = store;
        _productSvc   = productSvc;
        _inventorySvc = inventorySvc;
    }

    public IEnumerable<Order> GetAll() => _store.GetOrders();
    public IEnumerable<Order> GetByUser(string userId) => _store.GetOrdersByUser(userId);
    public Order? GetById(int id) => _store.GetOrder(id);

    public (Order? order, string? error) Create(string userId, string email, CreateOrderRequest request)
    {
        if (request.Items is null || request.Items.Count == 0)
            return (null, "Order must contain at least one item.");

        var orderItems = new List<OrderItem>();
        decimal total  = 0;

        // Validate stock for ALL items before touching inventory
        foreach (var lineItem in request.Items)
        {
            var product = _productSvc.GetById(lineItem.ProductId);
            if (product is null)
                return (null, $"Product {lineItem.ProductId} not found.");

            var inv = _inventorySvc.GetByProductId(lineItem.ProductId);
            if (inv is null || inv.Quantity < lineItem.Quantity)
                return (null, $"Insufficient stock for '{product.Name}'. Available: {inv?.Quantity ?? 0}.");

            orderItems.Add(new OrderItem
            {
                ProductId   = product.Id,
                ProductName = product.Name,
                Quantity    = lineItem.Quantity,
                UnitPrice   = product.Price
            });
            total += product.Price * lineItem.Quantity;
        }

        // Deduct inventory
        foreach (var lineItem in request.Items)
            _inventorySvc.Deduct(lineItem.ProductId, lineItem.Quantity);

        var order = _store.AddOrder(new Order
        {
            UserId          = userId,
            CustomerEmail   = email,
            Items           = orderItems,
            TotalAmount     = total,
            Status          = OrderStatus.Confirmed,
            ShippingAddress = request.ShippingAddress
        });

        return (order, null);
    }

    public bool UpdateStatus(int orderId, OrderStatus status)
        => _store.UpdateOrderStatus(orderId, status);
}

/// <summary>Auth service – registration, login, JWT token issuance.</summary>
public class AuthService : IAuthService
{
    private readonly InMemoryStore _store;
    private readonly IConfiguration _config;

    public AuthService(InMemoryStore store, IConfiguration config)
    {
        _store  = store;
        _config = config;
    }

    public (AppUser? user, string? error) Register(RegisterRequest request)
    {
        if (_store.FindUserByEmail(request.Email) is not null)
            return (null, "An account with that email already exists.");

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
            return (null, "Password must be at least 8 characters.");

        var user = _store.AddUser(new AppUser
        {
            Email        = request.Email.Trim().ToLower(),
            PasswordHash = InMemoryStore.HashPassword(request.Password),
            FullName     = request.FullName.Trim(),
            Role         = "Customer"
        });

        return (user, null);
    }

    public (AuthResponse? response, string? error) Login(LoginRequest request)
    {
        var user = _store.FindUserByEmail(request.Email);
        if (user is null || !InMemoryStore.VerifyPassword(request.Password, user.PasswordHash))
            return (null, "Invalid email or password.");

        var token     = GenerateJwt(user);
        var expiresAt = DateTime.UtcNow.AddMinutes(
            _config.GetValue<int>("Jwt:ExpiresInMinutes", 60));

        return (new AuthResponse
        {
            Token     = token,
            Email     = user.Email,
            FullName  = user.FullName,
            Role      = user.Role,
            ExpiresAt = expiresAt
        }, null);
    }

    // ─── JWT generation ──────────────────────────────────────────────────────
    private string GenerateJwt(AppUser user)
    {
        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var exp   = DateTime.UtcNow.AddMinutes(_config.GetValue<int>("Jwt:ExpiresInMinutes", 60));

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Email,          user.Email),
            new Claim(ClaimTypes.Name,           user.FullName),
            new Claim(ClaimTypes.Role,           user.Role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer:             _config["Jwt:Issuer"],
            audience:           _config["Jwt:Audience"],
            claims:             claims,
            expires:            exp,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

/// <summary>Aggregates store-wide statistics for the admin dashboard.</summary>
public class StatsService : IStatsService
{
    private readonly InMemoryStore _store;
    public StatsService(InMemoryStore store) => _store = store;

    public StoreStats GetStats()
    {
        var orders    = _store.GetOrders();
        var inventory = _store.GetInventory().Values.ToList();
        var products  = _store.GetProducts();

        var byStatus = orders
            .GroupBy(o => o.Status.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        var recent = orders
            .OrderByDescending(o => o.CreatedAt)
            .Take(5)
            .Select(o => new RecentOrder
            {
                Id            = o.Id,
                CustomerEmail = o.CustomerEmail,
                TotalAmount   = o.TotalAmount,
                Status        = o.Status.ToString(),
                CreatedAt     = o.CreatedAt
            }).ToList();

        return new StoreStats
        {
            TotalProducts   = products.Count,
            TotalOrders     = orders.Count,
            TotalRevenue    = orders.Where(o => o.Status != OrderStatus.Cancelled).Sum(o => o.TotalAmount),
            LowStockCount   = inventory.Count(i => i.Quantity > 0 && i.Quantity <= i.ReorderThreshold),
            OutOfStockCount = inventory.Count(i => i.Quantity <= 0),
            OrdersByStatus  = byStatus,
            RecentOrders    = recent
        };
    }
}
