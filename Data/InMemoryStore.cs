using Tecomm.Models;

namespace Tecomm.Data;

/// <summary>
/// Thread-safe in-memory store – replaces a real database for this demo.
/// Swap for EF Core DbContext in production.
/// </summary>
public class InMemoryStore
{
    // ─── Products ────────────────────────────────────────────────────────────
    private readonly List<Product> _products = new()
    {
        new() { Id = 1, Name = "Wireless Headphones", Description = "Premium noise-cancelling over-ear headphones with 30h battery", Price = 149.99m, Category = "Electronics", ImageUrl = "https://placehold.co/300x200/1a1a2e/white?text=Headphones" },
        new() { Id = 2, Name = "Mechanical Keyboard",  Description = "TKL mechanical keyboard with Cherry MX Red switches", Price = 89.99m,  Category = "Electronics", ImageUrl = "https://placehold.co/300x200/16213e/white?text=Keyboard" },
        new() { Id = 3, Name = "USB-C Hub",            Description = "7-in-1 USB-C hub with HDMI, SD card reader and 100W PD", Price = 49.99m,  Category = "Electronics", ImageUrl = "https://placehold.co/300x200/0f3460/white?text=USB-C+Hub" },
        new() { Id = 4, Name = "Laptop Stand",         Description = "Aluminium adjustable laptop stand, 10–15 inch",            Price = 39.99m,  Category = "Accessories", ImageUrl = "https://placehold.co/300x200/533483/white?text=Stand" },
        new() { Id = 5, Name = "Webcam HD 1080p",      Description = "Full HD webcam with built-in microphone and auto-focus",   Price = 69.99m,  Category = "Electronics", ImageUrl = "https://placehold.co/300x200/e94560/white?text=Webcam" },
        new() { Id = 6, Name = "Desk Mat XL",          Description = "Large 90×40 cm desk mat with non-slip rubber base",        Price = 24.99m,  Category = "Accessories", ImageUrl = "https://placehold.co/300x200/1a1a2e/white?text=Desk+Mat" },
    };

    // ─── Inventory ───────────────────────────────────────────────────────────
    private readonly Dictionary<int, InventoryItem> _inventory = new()
    {
        [1] = new() { ProductId = 1, Quantity = 50,  ReorderThreshold = 10 },
        [2] = new() { ProductId = 2, Quantity = 75,  ReorderThreshold = 15 },
        [3] = new() { ProductId = 3, Quantity = 120, ReorderThreshold = 20 },
        [4] = new() { ProductId = 4, Quantity = 40,  ReorderThreshold = 8  },
        [5] = new() { ProductId = 5, Quantity = 30,  ReorderThreshold = 5  },
        [6] = new() { ProductId = 6, Quantity = 200, ReorderThreshold = 25 },
    };

    // ─── Orders ──────────────────────────────────────────────────────────────
    private readonly List<Order> _orders = new();
    private int _nextOrderId = 1;

    // ─── Users ───────────────────────────────────────────────────────────────
    private readonly List<AppUser> _users = new()
    {
        // demo credentials  →  demo@tecomm.dev / Password1!
        new() {
            Id           = "seed-user-001",
            Email        = "demo@tecomm.dev",
            PasswordHash = BCryptHash("Password1!"),
            FullName     = "Demo User",
            Role         = "Customer"
        },
        new() {
            Id           = "seed-admin-001",
            Email        = "admin@tecomm.dev",
            PasswordHash = BCryptHash("Admin123!"),
            FullName     = "Store Admin",
            Role         = "Admin"
        }
    };

    // ─── Products ────────────────────────────────────────────────────────────
    public IReadOnlyList<Product> GetProducts() => _products.AsReadOnly();
    public Product? GetProduct(int id) => _products.FirstOrDefault(p => p.Id == id);

    public Product AddProduct(Product product)
    {
        product.Id = _products.Count > 0 ? _products.Max(p => p.Id) + 1 : 1;
        _products.Add(product);
        return product;
    }

    public bool DeleteProduct(int id)
    {
        var p = _products.FirstOrDefault(x => x.Id == id);
        if (p is null) return false;
        _products.Remove(p);
        _inventory.Remove(id);
        return true;
    }

    // ─── Inventory ───────────────────────────────────────────────────────────
    public IReadOnlyDictionary<int, InventoryItem> GetInventory() => _inventory;
    public InventoryItem? GetInventoryItem(int productId)
        => _inventory.TryGetValue(productId, out var item) ? item : null;

    public InventoryItem UpdateInventory(int productId, int quantity, int? reorderThreshold = null)
    {
        if (!_inventory.TryGetValue(productId, out var item))
        {
            item = new InventoryItem { ProductId = productId };
            _inventory[productId] = item;
        }
        item.Quantity         = quantity;
        item.LastUpdated      = DateTime.UtcNow;
        if (reorderThreshold.HasValue) item.ReorderThreshold = reorderThreshold.Value;
        return item;
    }

    public bool DeductInventory(int productId, int quantity)
    {
        if (!_inventory.TryGetValue(productId, out var item) || item.Quantity < quantity)
            return false;
        item.Quantity    -= quantity;
        item.LastUpdated  = DateTime.UtcNow;
        return true;
    }

    // ─── Orders ──────────────────────────────────────────────────────────────
    public IReadOnlyList<Order> GetOrders() => _orders.AsReadOnly();
    public IReadOnlyList<Order> GetOrdersByUser(string userId)
        => _orders.Where(o => o.UserId == userId).ToList().AsReadOnly();
    public Order? GetOrder(int id) => _orders.FirstOrDefault(o => o.Id == id);

    public Order AddOrder(Order order)
    {
        order.Id = _nextOrderId++;
        _orders.Add(order);
        return order;
    }

    public bool UpdateOrderStatus(int orderId, OrderStatus status)
    {
        var order = GetOrder(orderId);
        if (order is null) return false;
        order.Status = status;
        return true;
    }

    // ─── Users ───────────────────────────────────────────────────────────────
    public AppUser? FindUserByEmail(string email)
        => _users.FirstOrDefault(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase));

    public AppUser? FindUserById(string id)
        => _users.FirstOrDefault(u => u.Id == id);

    public AppUser AddUser(AppUser user)
    {
        _users.Add(user);
        return user;
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────
    // Simple deterministic "hash" for demo – replace with BCrypt NuGet in prod
    private static string BCryptHash(string password)
        => Convert.ToBase64String(
               System.Security.Cryptography.SHA256.HashData(
                   System.Text.Encoding.UTF8.GetBytes(password + "_tecomm_salt")));

    public static string HashPassword(string password) => BCryptHash(password);

    public static bool VerifyPassword(string password, string hash)
        => BCryptHash(password) == hash;
}
