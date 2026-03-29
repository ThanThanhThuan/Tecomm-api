# Tecomm — ASP.NET Core 8 Store API

A production-ready RESTful API for an online store with a built-in web frontend, JWT authentication, custom middleware, and full DI wiring.

---

## 🚀 Quick Start

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Run
```bash
cd Tecomm
dotnet restore
dotnet run
```

Open your browser:
| URL | Description |
|-----|-------------|
| `http://localhost:5000` | Web frontend (Shop, Orders, Inventory) |
| `http://localhost:5000/swagger` | Swagger / OpenAPI interactive docs |
| `http://localhost:5000/health` | Health check endpoint |

---

## 🏗️ Project Architecture

```
Tecomm/
├── Controllers/
│   ├── AuthController.cs        # POST /api/auth/register, /login
│   ├── ProductsController.cs    # GET  /api/products
│   ├── InventoryController.cs   # GET/PUT /api/inventory
│   └── OrdersController.cs      # POST/GET /api/orders
├── Middleware/
│   └── RequestLoggingMiddleware.cs  # Custom file-based request logger
├── Models/
│   └── Models.cs                # Domain models + DTOs
├── Services/
│   └── Services.cs              # Business logic (DI-registered)
├── Data/
│   └── InMemoryStore.cs         # Thread-safe in-memory data store
├── wwwroot/
│   ├── index.html               # SPA frontend
│   ├── css/style.css
│   └── js/app.js
├── Program.cs                   # App bootstrap, DI, middleware pipeline
├── appsettings.json
└── Tecomm.csproj
```

---

## 🔑 Core Requirements — Implemented

### 1. API Design
| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| GET | `/api/products` | — | List all products (`?category=` & `?search=`) |
| GET | `/api/products/{id}` | — | Single product |
| GET | `/api/products/categories` | — | Distinct categories |
| GET | `/api/inventory` | — | All inventory levels |
| GET | `/api/inventory/{productId}` | — | Single inventory item |
| **PUT** | **`/api/inventory/{productId}`** | Admin JWT | **Update stock** |
| GET | `/api/inventory/low-stock` | JWT | Items at/below reorder threshold |
| **POST** | **`/api/orders`** | JWT | **Checkout / Place order** |
| GET | `/api/orders/mine` | JWT | Caller's orders |
| GET | `/api/orders/{id}` | JWT | Single order |
| GET | `/api/orders` | Admin JWT | All orders |
| PUT | `/api/orders/{id}/status` | Admin JWT | Update order status |
| POST | `/api/auth/register` | — | Create account |
| POST | `/api/auth/login` | — | Login → JWT |
| GET | `/health` | — | Health check |

### 2. Dependency Injection
All services are registered in `Program.cs` using the built-in .NET DI container:

```csharp
builder.Services.AddSingleton<InMemoryStore>();          // shared state
builder.Services.AddScoped<IProductService,   ProductService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IOrderService,     OrderService>();
builder.Services.AddScoped<IAuthService,      AuthService>();
```

Each controller receives its dependencies via constructor injection.

### 3. Custom Request Logging Middleware
`RequestLoggingMiddleware` sits **first** in the pipeline and logs every request to:
- **Console** via structured `ILogger`
- **File**: `logs/requests.log` (auto-created, thread-safe, async)

Log format:
```
2024-01-15 10:23:45.123 UTC | POST   | 201 |    87ms | /api/orders | IP:127.0.0.1 | User:seed-user-001
```

Registration:
```csharp
app.UseRequestLogging(); // Custom extension method
```

### 4. JWT Authentication
- **Secured endpoint**: `POST /api/orders` — only authenticated users can checkout
- Tokens signed with HMAC-SHA256, configurable expiry (default 60 min)
- Returns clean JSON 401/403 instead of HTML challenges
- Swagger UI includes an **Authorize** button to test secured endpoints

**Token flow:**
```
POST /api/auth/login  →  { token, email, fullName, expiresAt }
Authorization: Bearer <token>  →  access secured endpoints
```

---

## 👤 Demo Credentials

| Role | Email | Password |
|------|-------|----------|
| Customer | `demo@tecomm.dev` | `Password1!` |
| Admin | `admin@tecomm.dev` | `Admin123!` |

Admin credentials unlock:
- `PUT /api/inventory/{id}` — update stock levels
- `GET /api/orders` — view all orders
- `PUT /api/orders/{id}/status` — manage order status

---

## ⚙️ Configuration (`appsettings.json`)

```json
{
  "Jwt": {
    "Key": "...",           // Change in production!
    "Issuer": "TecommAPI",
    "Audience": "TecommClient",
    "ExpiresInMinutes": 60
  },
  "RequestLogging": {
    "LogFilePath": "logs/requests.log"
  }
}
```

---

## 🧪 Testing the API with curl

```bash
# 1. Login
TOKEN=$(curl -s -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"demo@tecomm.dev","password":"Password1!"}' | grep -o '"token":"[^"]*"' | cut -d'"' -f4)

# 2. Browse products
curl http://localhost:5000/api/products

# 3. Checkout (JWT required)
curl -X POST http://localhost:5000/api/orders \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"items":[{"productId":1,"quantity":1}],"shippingAddress":"123 Main St"}'

# 4. Admin: update inventory
ADMIN_TOKEN=$(curl -s -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@tecomm.dev","password":"Admin123!"}' | grep -o '"token":"[^"]*"' | cut -d'"' -f4)

curl -X PUT http://localhost:5000/api/inventory/1 \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"quantity":100,"reorderThreshold":15}'
```

---

## 🔄 Production Checklist

- [ ] Replace `InMemoryStore` with Entity Framework Core + real database
- [ ] Use a strong, secrets-managed JWT key (Azure Key Vault / env vars)
- [ ] Replace the demo password hash with BCrypt (`BCrypt.Net-Next` NuGet)
- [ ] Add rate limiting (`builder.Services.AddRateLimiter(...)`)
- [ ] Configure HTTPS and HSTS
- [ ] Add integration tests with `WebApplicationFactory<Program>`
- [ ] Set up a rolling file logger (Serilog / NLog) for production log rotation
