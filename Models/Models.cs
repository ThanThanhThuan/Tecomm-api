namespace Tecomm.Models;

// ─── Product ────────────────────────────────────────────────────────────────
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Category { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
}

// ─── Inventory ───────────────────────────────────────────────────────────────
public class InventoryItem
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public int ReorderThreshold { get; set; } = 10;
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

// ─── Order ───────────────────────────────────────────────────────────────────
public class Order
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public List<OrderItem> Items { get; set; } = new();
    public decimal TotalAmount { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string ShippingAddress { get; set; } = string.Empty;
}

public class OrderItem
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public enum OrderStatus
{
    Pending,
    Confirmed,
    Shipped,
    Delivered,
    Cancelled
}

// ─── DTOs ─────────────────────────────────────────────────────────────────────
public class CreateOrderRequest
{
    public List<OrderItemRequest> Items { get; set; } = new();
    public string ShippingAddress { get; set; } = string.Empty;
}

public class OrderItemRequest
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
}

public class UpdateInventoryRequest
{
    public int Quantity { get; set; }
    public int? ReorderThreshold { get; set; }
}

public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class RegisterRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
}

public class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}

// ─── Product DTOs ─────────────────────────────────────────────────────────────
public class CreateProductRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Category { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public int InitialStock { get; set; } = 0;
    public int ReorderThreshold { get; set; } = 10;
}

// ─── Dashboard stats ──────────────────────────────────────────────────────────
public class StoreStats
{
    public int TotalProducts { get; set; }
    public int TotalOrders { get; set; }
    public decimal TotalRevenue { get; set; }
    public int LowStockCount { get; set; }
    public int OutOfStockCount { get; set; }
    public Dictionary<string, int> OrdersByStatus { get; set; } = new();
    public List<RecentOrder> RecentOrders { get; set; } = new();
}

public class RecentOrder
{
    public int Id { get; set; }
    public string CustomerEmail { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

// ─── User (in-memory) ────────────────────────────────────────────────────────
public class AppUser
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = "Customer";
}
