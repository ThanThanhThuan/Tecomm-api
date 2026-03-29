// ═══════════════════════════════════════════════════════════════════════════════
//  API HELPER
// ═══════════════════════════════════════════════════════════════════════════════
const API = {
  base: '/api',

  async request(method, path, body = null) {
    const headers = { 'Content-Type': 'application/json' };
    const token   = localStorage.getItem('tecomm_token');
    if (token) headers['Authorization'] = `Bearer ${token}`;

    const res  = await fetch(`${this.base}${path}`, {
      method, headers,
      body: body ? JSON.stringify(body) : null
    });

    const json = await res.json().catch(() => null);
    if (!res.ok) throw new Error(json?.message || `HTTP ${res.status}`);
    return json;
  },

  get:    (path)       => API.request('GET',    path),
  post:   (path, body) => API.request('POST',   path, body),
  put:    (path, body) => API.request('PUT',    path, body),
  delete: (path)       => API.request('DELETE', path),
};

// ═══════════════════════════════════════════════════════════════════════════════
//  STATE
// ═══════════════════════════════════════════════════════════════════════════════
const state = {
  products:        [],
  inventory:       {},
  cart:            JSON.parse(localStorage.getItem('tecomm_cart') || '[]'),
  user:            JSON.parse(localStorage.getItem('tecomm_user') || 'null'),
  detailProductId: null,
  detailQty:       1,
};

// ═══════════════════════════════════════════════════════════════════════════════
//  TOAST
// ═══════════════════════════════════════════════════════════════════════════════
let toastTimer;
function toast(msg, type = 'info') {
  const el = document.getElementById('toast');
  el.textContent = msg;
  el.className   = `toast ${type}`;
  el.classList.remove('hidden');
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => el.classList.add('hidden'), 3400);
}

// ═══════════════════════════════════════════════════════════════════════════════
//  AUTH
// ═══════════════════════════════════════════════════════════════════════════════
function isAdmin() { return state.user?.role === 'Admin'; }

function updateAuthUI() {
  const loggedIn = !!state.user;
  document.getElementById('authArea').classList.toggle('hidden',  loggedIn);
  document.getElementById('userArea').classList.toggle('hidden', !loggedIn);

  if (loggedIn) {
    const pill = document.getElementById('userName');
    pill.textContent = isAdmin()
      ? `${state.user.fullName} (Admin)`
      : state.user.fullName;
  }

  // Show / hide admin nav tabs
  ['navDashboard','navManage','navAllOrders'].forEach(id =>
    document.getElementById(id).classList.toggle('hidden', !isAdmin()));
}

function logout() {
  localStorage.removeItem('tecomm_token');
  localStorage.removeItem('tecomm_user');
  state.user = null;
  updateAuthUI();
  switchView('shop');
  toast('Logged out successfully.');
}

async function login() {
  const email    = document.getElementById('loginEmail').value.trim();
  const password = document.getElementById('loginPassword').value;
  const errEl    = document.getElementById('loginError');
  errEl.classList.add('hidden');
  try {
    const res = await API.post('/auth/login', { email, password });
    localStorage.setItem('tecomm_token', res.token);
    localStorage.setItem('tecomm_user',
      JSON.stringify({ email: res.email, fullName: res.fullName, role: res.role }));
    state.user = { email: res.email, fullName: res.fullName, role: res.role };
    updateAuthUI();
    closeModal('loginModal');
    toast(`Welcome back, ${res.fullName}!`, 'success');
    renderProducts(state.products); // refresh Add-to-Cart buttons etc.
  } catch (e) {
    errEl.textContent = e.message;
    errEl.classList.remove('hidden');
  }
}

async function register() {
  const name     = document.getElementById('regName').value.trim();
  const email    = document.getElementById('regEmail').value.trim();
  const password = document.getElementById('regPassword').value;
  const errEl    = document.getElementById('registerError');
  errEl.classList.add('hidden');
  try {
    const res = await API.post('/auth/register', { fullName: name, email, password });
    localStorage.setItem('tecomm_token', res.token);
    localStorage.setItem('tecomm_user',
      JSON.stringify({ email: res.email, fullName: res.fullName, role: res.role }));
    state.user = { email: res.email, fullName: res.fullName, role: res.role };
    updateAuthUI();
    closeModal('registerModal');
    toast(`Account created! Welcome, ${res.fullName}!`, 'success');
  } catch (e) {
    errEl.textContent = e.message;
    errEl.classList.remove('hidden');
  }
}

// ═══════════════════════════════════════════════════════════════════════════════
//  MODALS
// ═══════════════════════════════════════════════════════════════════════════════
function openModal(id) {
  document.getElementById(id).classList.remove('hidden');
  document.getElementById('modalBackdrop').classList.remove('hidden');
}
function closeModal(id) {
  document.getElementById(id).classList.add('hidden');
  // Only hide backdrop if no other modals are open
  const anyOpen = ['loginModal','registerModal','productModal','addProductModal']
    .some(m => !document.getElementById(m).classList.contains('hidden'));
  if (!anyOpen) document.getElementById('modalBackdrop').classList.add('hidden');
}

// ═══════════════════════════════════════════════════════════════════════════════
//  SHOP — PRODUCTS
// ═══════════════════════════════════════════════════════════════════════════════
async function loadProducts() {
  try {
    state.products  = await API.get('/products');
    const inv       = await API.get('/inventory');
    state.inventory = Object.fromEntries(inv.map(i => [i.productId, i]));
    await loadCategories();
    renderProducts(state.products);
  } catch (e) {
    document.getElementById('productGrid').innerHTML =
      `<p class="empty-state">Failed to load products: ${e.message}</p>`;
  }
}

async function loadCategories() {
  const cats = await API.get('/products/categories').catch(() => []);
  const sel  = document.getElementById('categoryFilter');
  // Clear old options except first
  while (sel.options.length > 1) sel.remove(1);
  cats.forEach(c => {
    const opt = document.createElement('option');
    opt.value = c; opt.textContent = c;
    sel.appendChild(opt);
  });
}

function getStockBadge(productId) {
  const inv = state.inventory[productId];
  if (!inv || inv.quantity <= 0)
    return `<span class="stock-badge stock-out">Out of stock</span>`;
  if (inv.quantity <= inv.reorderThreshold)
    return `<span class="stock-badge stock-low">Low stock (${inv.quantity})</span>`;
  return `<span class="stock-badge stock-ok">In stock (${inv.quantity})</span>`;
}

function renderProducts(products) {
  const grid = document.getElementById('productGrid');
  if (!products.length) {
    grid.innerHTML = '<p class="empty-state">No products found.</p>';
    return;
  }
  grid.innerHTML = products.map(p => {
    const outOfStock = (state.inventory[p.id]?.quantity ?? 0) <= 0;
    return `
    <div class="product-card" onclick="openProductDetail(${p.id})" style="cursor:pointer">
      <img class="product-img" src="${p.imageUrl}" alt="${p.name}" loading="lazy" />
      <div class="product-body">
        <div class="product-category">${p.category}</div>
        <div class="product-name">${p.name}</div>
        <div class="product-desc">${p.description}</div>
      </div>
      <div class="product-footer">
        <div>
          <div class="product-price">$${p.price.toFixed(2)}</div>
          ${getStockBadge(p.id)}
        </div>
        <button class="btn btn-primary btn-sm"
          onclick="event.stopPropagation(); addToCart(${p.id})"
          ${outOfStock ? 'disabled' : ''}>
          Add to Cart
        </button>
      </div>
    </div>`;
  }).join('');
}

function filterProducts() {
  const search   = document.getElementById('searchInput').value.toLowerCase();
  const category = document.getElementById('categoryFilter').value;
  renderProducts(state.products.filter(p => {
    const matchSearch   = !search   || p.name.toLowerCase().includes(search) || p.description.toLowerCase().includes(search);
    const matchCategory = !category || p.category === category;
    return matchSearch && matchCategory;
  }));
}

// ═══════════════════════════════════════════════════════════════════════════════
//  PRODUCT DETAIL MODAL
// ═══════════════════════════════════════════════════════════════════════════════
function openProductDetail(productId) {
  const p   = state.products.find(x => x.id === productId);
  if (!p) return;
  const inv = state.inventory[productId];

  state.detailProductId = productId;
  state.detailQty       = 1;

  document.getElementById('productModalName').textContent     = p.name;
  document.getElementById('productModalImg').src              = p.imageUrl;
  document.getElementById('productModalImg').alt              = p.name;
  document.getElementById('productModalCategory').textContent = p.category;
  document.getElementById('productModalDesc').textContent     = p.description;
  document.getElementById('productModalPrice').textContent    = `$${p.price.toFixed(2)}`;
  document.getElementById('productModalStock').innerHTML      = getStockBadge(productId);
  document.getElementById('detailQty').textContent            = '1';

  const addBtn = document.getElementById('productModalAddBtn');
  const outOfStock = !inv || inv.quantity <= 0;
  addBtn.disabled    = outOfStock;
  addBtn.textContent = outOfStock ? 'Out of Stock' : 'Add to Cart';

  openModal('productModal');
}

function detailQtyChange(delta) {
  const inv    = state.inventory[state.detailProductId];
  const maxQty = inv?.quantity ?? 0;
  state.detailQty = Math.max(1, Math.min(state.detailQty + delta, maxQty));
  document.getElementById('detailQty').textContent = state.detailQty;
}

function addFromDetail() {
  const productId = state.detailProductId;
  const qty       = state.detailQty;
  const product   = state.products.find(p => p.id === productId);
  if (!product) return;

  const inv      = state.inventory[productId];
  const existing = state.cart.find(i => i.productId === productId);
  const inCart   = existing ? existing.quantity : 0;

  if (inv && inCart + qty > inv.quantity) {
    toast('Not enough stock available.', 'error'); return;
  }

  if (existing) existing.quantity += qty;
  else state.cart.push({ productId, name: product.name, price: product.price, quantity: qty });

  saveCart();
  updateCartBadge();
  closeModal('productModal');
  toast(`${product.name} ×${qty} added to cart!`, 'success');
}

// ═══════════════════════════════════════════════════════════════════════════════
//  CART
// ═══════════════════════════════════════════════════════════════════════════════
function saveCart() { localStorage.setItem('tecomm_cart', JSON.stringify(state.cart)); }

function addToCart(productId) {
  const product  = state.products.find(p => p.id === productId);
  if (!product) return;
  const inv      = state.inventory[productId];
  const existing = state.cart.find(i => i.productId === productId);
  const inCart   = existing ? existing.quantity : 0;
  if (inv && inCart >= inv.quantity) { toast('Not enough stock available.', 'error'); return; }
  if (existing) existing.quantity++;
  else state.cart.push({ productId, name: product.name, price: product.price, quantity: 1 });
  saveCart(); updateCartBadge();
  toast(`${product.name} added to cart!`, 'success');
}

function removeFromCart(productId) {
  state.cart = state.cart.filter(i => i.productId !== productId);
  saveCart(); updateCartBadge(); renderCart();
}

function updateCartQty(productId, delta) {
  const item = state.cart.find(i => i.productId === productId);
  if (!item) return;
  item.quantity += delta;
  if (item.quantity <= 0) removeFromCart(productId);
  else { saveCart(); updateCartBadge(); renderCart(); }
}

function updateCartBadge() {
  document.getElementById('cartCount').textContent =
    state.cart.reduce((s, i) => s + i.quantity, 0);
}

function cartTotal() { return state.cart.reduce((s, i) => s + i.price * i.quantity, 0); }

function renderCart() {
  const el = document.getElementById('cartItems');
  if (!state.cart.length) {
    el.innerHTML = '<p class="empty-state">Your cart is empty.</p>';
    document.getElementById('cartTotal').textContent = '$0.00';
    return;
  }
  el.innerHTML = state.cart.map(i => `
    <div class="cart-item">
      <div class="cart-item-info">
        <div class="cart-item-name">${i.name}</div>
        <div class="cart-item-price">$${i.price.toFixed(2)} each · $${(i.price * i.quantity).toFixed(2)}</div>
        <div class="cart-item-qty">
          <button class="qty-btn" onclick="updateCartQty(${i.productId},-1)">−</button>
          <span>${i.quantity}</span>
          <button class="qty-btn" onclick="updateCartQty(${i.productId}, 1)">+</button>
          <button class="qty-btn" onclick="removeFromCart(${i.productId})"
            style="margin-left:auto;background:rgba(248,113,113,.2);color:#f87171">✕</button>
        </div>
      </div>
    </div>`).join('');
  document.getElementById('cartTotal').textContent = `$${cartTotal().toFixed(2)}`;
}

function openCart()  { renderCart(); document.getElementById('cartDrawer').classList.remove('hidden'); document.getElementById('cartOverlay').classList.remove('hidden'); }
function closeCart() { document.getElementById('cartDrawer').classList.add('hidden');    document.getElementById('cartOverlay').classList.add('hidden'); }

async function checkout() {
  if (!state.user) { closeCart(); openModal('loginModal'); toast('Please log in to complete your purchase.', 'error'); return; }
  if (!state.cart.length) { toast('Your cart is empty.', 'error'); return; }
  const address = document.getElementById('shippingAddress').value.trim();
  if (!address)  { toast('Please enter a shipping address.', 'error'); return; }

  const btn = document.getElementById('checkoutBtn');
  btn.disabled = true; btn.textContent = 'Placing order…';

  try {
    const order = await API.post('/orders', {
      items:           state.cart.map(i => ({ productId: i.productId, quantity: i.quantity })),
      shippingAddress: address
    });
    state.cart = []; saveCart(); updateCartBadge(); closeCart();
    toast(`✓ Order #${order.id} confirmed! Total: $${order.totalAmount.toFixed(2)}`, 'success');
    const inv = await API.get('/inventory');
    state.inventory = Object.fromEntries(inv.map(i => [i.productId, i]));
    renderProducts(state.products);
  } catch (e) {
    toast(e.message, 'error');
  } finally {
    btn.disabled = false; btn.textContent = 'Checkout';
  }
}

// ═══════════════════════════════════════════════════════════════════════════════
//  MY ORDERS
// ═══════════════════════════════════════════════════════════════════════════════
async function loadOrders() {
  const el = document.getElementById('ordersContent');
  if (!state.user) { el.innerHTML = '<p class="empty-state">Please log in to view your orders.</p>'; return; }
  el.innerHTML = '<div class="loading">Loading…</div>';
  try {
    const orders = await API.get('/orders/mine');
    if (!orders.length) { el.innerHTML = '<p class="empty-state">You have no orders yet.</p>'; return; }
    el.innerHTML = orders.map(o => `
      <div class="order-card">
        <div class="order-meta">
          <span class="order-id">Order #${o.id}</span>
          <span class="order-date">${new Date(o.createdAt).toLocaleDateString('en-US',{year:'numeric',month:'short',day:'numeric'})}</span>
          <span class="order-status status-${o.status.toLowerCase()}">${o.status}</span>
        </div>
        <div class="order-items" style="margin:.5rem 0">
          ${o.items.map(i => `<span style="display:inline-block;background:var(--surface2);border:1px solid var(--border);border-radius:6px;padding:.2rem .55rem;font-size:.8rem;margin:.15rem">${i.productName} ×${i.quantity}</span>`).join('')}
        </div>
        <div style="font-size:.85rem;color:var(--text-muted)">📦 ${o.shippingAddress}</div>
        <div class="order-total" style="margin-top:.5rem">Total: $${o.totalAmount.toFixed(2)}</div>
      </div>`).join('');
  } catch (e) {
    el.innerHTML = `<p class="empty-state">Failed to load orders: ${e.message}</p>`;
  }
}

// ═══════════════════════════════════════════════════════════════════════════════
//  INVENTORY
// ═══════════════════════════════════════════════════════════════════════════════
async function loadInventoryView() {
  const el = document.getElementById('inventoryContent');
  el.innerHTML = '<div class="loading">Loading inventory…</div>';
  try {
    const inv = await API.get('/inventory');
    if (!state.products.length) state.products = await API.get('/products');
    const map     = Object.fromEntries(state.products.map(p => [p.id, p]));
    const canEdit = !!localStorage.getItem('tecomm_token');

    el.innerHTML = `
      <div style="overflow-x:auto;border-radius:var(--radius);border:1px solid var(--border)">
        <table class="inventory-table">
          <thead><tr>
            <th>Product</th><th>Category</th><th>Qty</th><th>Reorder At</th><th>Status</th><th>Updated</th>
            ${canEdit ? '<th>Save</th>' : ''}
          </tr></thead>
          <tbody>${inv.map(i => {
            const p        = map[i.productId] || {};
            const qtyClass = i.quantity <= 0 ? 'qty-out' : i.quantity <= i.reorderThreshold ? 'qty-low' : 'qty-ok';
            const badge    = i.quantity <= 0 ? 'stock-out' : i.quantity <= i.reorderThreshold ? 'stock-low' : 'stock-ok';
            const label    = i.quantity <= 0 ? 'Out of stock' : i.quantity <= i.reorderThreshold ? 'Low stock' : 'OK';
            return `<tr>
              <td><strong>${p.name || `#${i.productId}`}</strong></td>
              <td style="color:var(--text-muted)">${p.category || '—'}</td>
              <td class="${qtyClass}">
                ${canEdit
                  ? `<input id="qty-${i.productId}" type="number" class="input" style="width:75px;padding:.3rem .45rem" value="${i.quantity}" min="0"/>`
                  : `<strong>${i.quantity}</strong>`}
              </td>
              <td>
                ${canEdit
                  ? `<input id="thr-${i.productId}" type="number" class="input" style="width:75px;padding:.3rem .45rem" value="${i.reorderThreshold}" min="0"/>`
                  : i.reorderThreshold}
              </td>
              <td><span class="stock-badge ${badge}">${label}</span></td>
              <td style="color:var(--text-muted);font-size:.8rem">${new Date(i.lastUpdated).toLocaleString()}</td>
              ${canEdit ? `<td><button class="btn btn-primary btn-sm" onclick="saveInventory(${i.productId})">Save</button></td>` : ''}
            </tr>`;
          }).join('')}</tbody>
        </table>
      </div>`;
  } catch (e) {
    el.innerHTML = `<p class="empty-state">Failed to load inventory: ${e.message}</p>`;
  }
}

async function saveInventory(productId) {
  const qty = parseInt(document.getElementById(`qty-${productId}`)?.value, 10);
  const thr = parseInt(document.getElementById(`thr-${productId}`)?.value, 10);
  if (isNaN(qty) || qty < 0) { toast('Quantity must be 0 or more.', 'error'); return; }
  try {
    await API.put(`/inventory/${productId}`, { quantity: qty, reorderThreshold: isNaN(thr) ? undefined : thr });
    // Update local inventory cache
    if (state.inventory[productId]) {
      state.inventory[productId].quantity         = qty;
      state.inventory[productId].reorderThreshold = isNaN(thr) ? state.inventory[productId].reorderThreshold : thr;
    }
    toast('Inventory updated!', 'success');
    await loadInventoryView();
  } catch (e) {
    toast(e.message, 'error');
  }
}

// ═══════════════════════════════════════════════════════════════════════════════
//  ADMIN: DASHBOARD
// ═══════════════════════════════════════════════════════════════════════════════
async function loadDashboard() {
  const el = document.getElementById('dashboardContent');
  el.innerHTML = '<div class="loading">Loading stats…</div>';
  try {
    const s = await API.get('/dashboard');
    const totalOrders = s.totalOrders || 1; // avoid div-by-zero
    const statusColors = {
      Confirmed: 'var(--success)', Pending: 'var(--warning)',
      Shipped: 'var(--accent-h)', Delivered: 'var(--success)', Cancelled: 'var(--error)'
    };

    el.innerHTML = `
      <div class="stats-grid">
        <div class="stat-card accent">
          <div class="stat-label">Total Revenue</div>
          <div class="stat-value">$${s.totalRevenue.toFixed(0)}</div>
        </div>
        <div class="stat-card success">
          <div class="stat-label">Total Orders</div>
          <div class="stat-value">${s.totalOrders}</div>
        </div>
        <div class="stat-card">
          <div class="stat-label">Products</div>
          <div class="stat-value">${s.totalProducts}</div>
        </div>
        <div class="stat-card warning">
          <div class="stat-label">Low Stock SKUs</div>
          <div class="stat-value">${s.lowStockCount}</div>
        </div>
        <div class="stat-card error">
          <div class="stat-label">Out of Stock</div>
          <div class="stat-value">${s.outOfStockCount}</div>
        </div>
      </div>

      <div class="dashboard-row">
        <div class="dashboard-card">
          <h3>Orders by Status</h3>
          ${Object.entries(s.ordersByStatus).map(([status, count]) => `
            <div class="status-bar-row">
              <span class="status-bar-label">${status}</span>
              <div class="status-bar-track">
                <div class="status-bar-fill" style="width:${Math.round(count/totalOrders*100)}%;background:${statusColors[status]||'var(--accent)'}"></div>
              </div>
              <span class="status-bar-count">${count}</span>
            </div>`).join('')}
          ${Object.keys(s.ordersByStatus).length === 0 ? '<p style="color:var(--text-muted);font-size:.88rem">No orders yet.</p>' : ''}
        </div>

        <div class="dashboard-card">
          <h3>Recent Orders</h3>
          ${s.recentOrders.length === 0
            ? '<p style="color:var(--text-muted);font-size:.88rem">No orders yet.</p>'
            : s.recentOrders.map(o => `
            <div class="recent-order-row">
              <div>
                <div style="font-weight:600;font-size:.88rem">#${o.id} · ${o.customerEmail}</div>
                <div style="font-size:.78rem;color:var(--text-muted)">${new Date(o.createdAt).toLocaleDateString()}</div>
              </div>
              <div style="text-align:right">
                <div style="font-weight:700;color:var(--accent-h)">$${o.totalAmount.toFixed(2)}</div>
                <span class="order-status status-${o.status.toLowerCase()}" style="font-size:.72rem">${o.status}</span>
              </div>
            </div>`).join('')}
        </div>
      </div>

      <div style="margin-top:1.25rem">
        <button class="btn btn-outline btn-sm" onclick="loadDashboard()">↻ Refresh</button>
      </div>`;
  } catch (e) {
    el.innerHTML = `<p class="empty-state">Failed to load dashboard: ${e.message}</p>`;
  }
}

// ═══════════════════════════════════════════════════════════════════════════════
//  ADMIN: MANAGE PRODUCTS
// ═══════════════════════════════════════════════════════════════════════════════
async function loadManageProducts() {
  const el = document.getElementById('manageContent');
  el.innerHTML = '<div class="loading">Loading…</div>';
  try {
    if (!state.products.length) state.products = await API.get('/products');
    const inv = await API.get('/inventory');
    state.inventory = Object.fromEntries(inv.map(i => [i.productId, i]));
    renderManageTable();
  } catch (e) {
    el.innerHTML = `<p class="empty-state">Failed: ${e.message}</p>`;
  }
}

function renderManageTable() {
  const el = document.getElementById('manageContent');
  if (!state.products.length) {
    el.innerHTML = '<p class="empty-state">No products yet. Add one!</p>'; return;
  }
  el.innerHTML = `
    <div style="overflow-x:auto;border-radius:var(--radius);border:1px solid var(--border)">
      <table class="manage-table">
        <thead><tr>
          <th>Img</th><th>Name</th><th>Category</th>
          <th>Price</th><th>Stock</th><th>Actions</th>
        </tr></thead>
        <tbody>${state.products.map(p => {
          const inv   = state.inventory[p.id];
          const stock = inv?.quantity ?? '—';
          const sc    = !inv ? '' : inv.quantity <= 0 ? 'qty-out' : inv.quantity <= inv.reorderThreshold ? 'qty-low' : 'qty-ok';
          return `<tr>
            <td><img src="${p.imageUrl}" alt="${p.name}" /></td>
            <td><strong>${p.name}</strong><br/><span style="font-size:.8rem;color:var(--text-muted)">#${p.id}</span></td>
            <td><span class="product-category">${p.category}</span></td>
            <td style="font-weight:700;color:var(--accent-h)">$${p.price.toFixed(2)}</td>
            <td class="${sc}"><strong>${stock}</strong></td>
            <td>
              <button class="btn btn-danger btn-sm" onclick="deleteProduct(${p.id},'${p.name.replace(/'/g,"\\'")}')">Delete</button>
            </td>
          </tr>`;
        }).join('')}</tbody>
      </table>
    </div>`;
}

async function deleteProduct(id, name) {
  if (!confirm(`Delete "${name}"? This also removes its inventory record.`)) return;
  try {
    await API.delete(`/products/${id}`);
    state.products = state.products.filter(p => p.id !== id);
    delete state.inventory[id];
    toast(`"${name}" deleted.`, 'success');
    renderManageTable();
    // Also refresh the shop grid
    renderProducts(state.products);
    loadCategories();
  } catch (e) {
    toast(e.message, 'error');
  }
}

async function submitAddProduct() {
  const errEl = document.getElementById('addProductError');
  errEl.classList.add('hidden');

  const name      = document.getElementById('apName').value.trim();
  const category  = document.getElementById('apCategory').value.trim();
  const price     = parseFloat(document.getElementById('apPrice').value);
  const stock     = parseInt(document.getElementById('apStock').value, 10);
  const threshold = parseInt(document.getElementById('apThreshold').value, 10);
  const image     = document.getElementById('apImage').value.trim();
  const desc      = document.getElementById('apDesc').value.trim();

  if (!name || !category || !desc || isNaN(price) || price <= 0) {
    errEl.textContent = 'Name, category, description and a positive price are required.';
    errEl.classList.remove('hidden'); return;
  }

  const btn = document.getElementById('addProductSubmit');
  btn.disabled = true; btn.textContent = 'Creating…';

  try {
    const product = await API.post('/products', {
      name, category, description: desc, price,
      initialStock:    isNaN(stock)     ? 0  : stock,
      reorderThreshold: isNaN(threshold) ? 10 : threshold,
      imageUrl: image
    });

    // Refresh local state
    state.products.push(product);
    const freshInv = await API.get('/inventory');
    state.inventory = Object.fromEntries(freshInv.map(i => [i.productId, i]));

    closeModal('addProductModal');
    toast(`"${product.name}" created!`, 'success');

    // Reset form
    ['apName','apCategory','apPrice','apImage','apDesc'].forEach(id => document.getElementById(id).value = '');
    document.getElementById('apStock').value     = '50';
    document.getElementById('apThreshold').value = '10';

    renderManageTable();
    renderProducts(state.products);
    loadCategories();
  } catch (e) {
    errEl.textContent = e.message;
    errEl.classList.remove('hidden');
  } finally {
    btn.disabled = false; btn.textContent = 'Create Product';
  }
}

// ═══════════════════════════════════════════════════════════════════════════════
//  ADMIN: ALL ORDERS
// ═══════════════════════════════════════════════════════════════════════════════
const ORDER_STATUSES = ['Pending','Confirmed','Shipped','Delivered','Cancelled'];

async function loadAllOrders() {
  const el = document.getElementById('allOrdersContent');
  el.innerHTML = '<div class="loading">Loading orders…</div>';
  try {
    const orders = await API.get('/orders');
    if (!orders.length) { el.innerHTML = '<p class="empty-state">No orders yet.</p>'; return; }

    el.innerHTML = `
      <div style="overflow-x:auto;border-radius:var(--radius);border:1px solid var(--border)">
        <table class="orders-table">
          <thead><tr>
            <th>#</th><th>Customer</th><th>Items</th>
            <th>Total</th><th>Date</th><th>Status</th>
          </tr></thead>
          <tbody>${orders.map(o => `
            <tr>
              <td><strong>#${o.id}</strong></td>
              <td>
                <div style="font-size:.88rem">${o.customerEmail}</div>
                <div style="font-size:.78rem;color:var(--text-muted)">${o.shippingAddress}</div>
              </td>
              <td style="font-size:.82rem;color:var(--text-muted)">
                ${o.items.map(i => `${i.productName} ×${i.quantity}`).join(', ')}
              </td>
              <td style="font-weight:700;color:var(--accent-h)">$${o.totalAmount.toFixed(2)}</td>
              <td style="font-size:.82rem;color:var(--text-muted)">
                ${new Date(o.createdAt).toLocaleDateString('en-US',{month:'short',day:'numeric',year:'numeric'})}
              </td>
              <td>
                <select class="status-select" onchange="updateOrderStatus(${o.id}, this.value)">
                  ${ORDER_STATUSES.map(s =>
                    `<option value="${s}" ${s === o.status ? 'selected' : ''}>${s}</option>`
                  ).join('')}
                </select>
              </td>
            </tr>`).join('')}
          </tbody>
        </table>
      </div>
      <div style="margin-top:1rem">
        <button class="btn btn-outline btn-sm" onclick="loadAllOrders()">↻ Refresh</button>
      </div>`;
  } catch (e) {
    el.innerHTML = `<p class="empty-state">Failed to load orders: ${e.message}</p>`;
  }
}

async function updateOrderStatus(orderId, status) {
  try {
    await API.put(`/orders/${orderId}/status`, { status });
    toast(`Order #${orderId} → ${status}`, 'success');
  } catch (e) {
    toast(e.message, 'error');
    loadAllOrders(); // revert dropdown on failure
  }
}

// ═══════════════════════════════════════════════════════════════════════════════
//  NAVIGATION
// ═══════════════════════════════════════════════════════════════════════════════
function switchView(name) {
  document.querySelectorAll('.view').forEach(v => {
    v.classList.remove('active');
    v.classList.add('hidden');
  });
  document.querySelectorAll('.nav-btn').forEach(b => b.classList.remove('active'));

  const view = document.getElementById(`view-${name}`);
  if (!view) return;
  view.classList.remove('hidden');
  view.classList.add('active');

  const btn = document.querySelector(`[data-view="${name}"]`);
  if (btn) btn.classList.add('active');

  // Load data on tab activation
  const loaders = {
    orders:    loadOrders,
    inventory: loadInventoryView,
    dashboard: loadDashboard,
    manage:    loadManageProducts,
    allorders: loadAllOrders,
  };
  if (loaders[name]) loaders[name]();
}

// ═══════════════════════════════════════════════════════════════════════════════
//  INIT
// ═══════════════════════════════════════════════════════════════════════════════
document.addEventListener('DOMContentLoaded', () => {
  updateAuthUI();
  updateCartBadge();
  loadProducts();

  // Ensure only shop is shown initially
  document.querySelectorAll('.view').forEach(v => {
    if (v.id !== 'view-shop') { v.classList.add('hidden'); v.classList.remove('active'); }
  });

  // Nav buttons
  document.querySelectorAll('.nav-btn').forEach(btn =>
    btn.addEventListener('click', () => switchView(btn.dataset.view)));

  // Auth
  document.getElementById('loginBtn').addEventListener('click',       () => openModal('loginModal'));
  document.getElementById('registerBtn').addEventListener('click',    () => openModal('registerModal'));
  document.getElementById('logoutBtn').addEventListener('click',      logout);
  document.getElementById('loginSubmit').addEventListener('click',    login);
  document.getElementById('registerSubmit').addEventListener('click', register);
  document.getElementById('switchToRegister').addEventListener('click', e => {
    e.preventDefault(); closeModal('loginModal'); openModal('registerModal');
  });
  document.getElementById('switchToLogin').addEventListener('click', e => {
    e.preventDefault(); closeModal('registerModal'); openModal('loginModal');
  });

  // Enter key in auth forms
  document.getElementById('loginPassword').addEventListener('keydown',  e => e.key === 'Enter' && login());
  document.getElementById('regPassword').addEventListener('keydown',    e => e.key === 'Enter' && register());

  // Close modals
  document.querySelectorAll('[data-close]').forEach(btn =>
    btn.addEventListener('click', () => closeModal(btn.dataset.close)));
  document.getElementById('modalBackdrop').addEventListener('click', () => {
    ['loginModal','registerModal','productModal','addProductModal']
      .forEach(id => closeModal(id));
  });

  // Cart
  document.getElementById('cartBtn').addEventListener('click',     openCart);
  document.getElementById('closeCart').addEventListener('click',   closeCart);
  document.getElementById('cartOverlay').addEventListener('click', closeCart);
  document.getElementById('checkoutBtn').addEventListener('click', checkout);

  // Product filters
  document.getElementById('searchInput').addEventListener('input',     filterProducts);
  document.getElementById('categoryFilter').addEventListener('change', filterProducts);

  // Admin: Add Product
  document.getElementById('addProductBtn').addEventListener('click',    () => openModal('addProductModal'));
  document.getElementById('addProductSubmit').addEventListener('click', submitAddProduct);
  document.getElementById('apDesc').addEventListener('keydown', e => {
    if (e.key === 'Enter' && e.ctrlKey) submitAddProduct();
  });
});
