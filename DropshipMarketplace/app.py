#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
پلتفرم فروش بدون سرمایه (Dropshipping Marketplace) — MVP مرحله اول (مزارشریف)
Backend: Flask + SQLite | Frontend: static HTML/CSS/JS (RTL, Dari/FA)

Money model (per order line):
    sale_unit      = wholesale_price + seller_margin
    sale_subtotal  = sale_unit * qty
    delivery_fee   = DELIVERY_FEE (flat, Phase 1: Mazar only)
    customer_pays  = sale_subtotal + delivery_fee          (Cash on Delivery)
    seller_earning = seller_margin * qty                   (paid after delivery)
    supplier_share = wholesale_price * qty
    platform_fee   = round(wholesale_price * qty * PLATFORM_RATE)
"""
import os
import re
import secrets
import sqlite3
from datetime import datetime
from functools import wraps

from flask import Flask, jsonify, request, g, send_from_directory

BASE_DIR = os.path.dirname(os.path.abspath(__file__))
DB_PATH = os.path.join(BASE_DIR, "marketplace.db")
STATIC_DIR = os.path.join(BASE_DIR, "static")

DELIVERY_FEE = 100          # AFN, flat — Phase 1 (Mazar-i-Sharif only)
PLATFORM_RATE = 0.05        # 5% of wholesale subtotal
FREE_PLAN_LIMIT = 20        # free sellers can pick max 20 products
CITIES = ["مزارشریف", "کابل", "هرات", "قندهار", "بلخ", "ننگرهار", "بدخشان", "غزنی", "بامیان", "فراه"]

ORDER_FLOW = {
    "pending": ["confirmed", "cancelled"],
    "confirmed": ["shipped", "cancelled"],
    "shipped": ["delivered", "returned"],
    "delivered": [],
    "cancelled": [],
    "returned": [],
}
ACTIVE_STATUSES = ("pending", "confirmed", "shipped")

app = Flask(__name__, static_folder=STATIC_DIR, static_url_path="")


# ---------------- DB helpers ----------------
def get_db():
    if "db" not in g:
        g.db = sqlite3.connect(DB_PATH)
        g.db.row_factory = sqlite3.Row
        g.db.execute("PRAGMA foreign_keys = ON")
    return g.db


@app.teardown_appcontext
def close_db(exc=None):
    db = g.pop("db", None)
    if db is not None:
        db.close()


def q(query, args=(), one=False):
    cur = get_db().execute(query, args)
    rows = cur.fetchall()
    cur.close()
    if one:
        return dict(rows[0]) if rows else None
    return [dict(r) for r in rows]


def exec_write(query, args=()):
    db = get_db()
    cur = db.execute(query, args)
    db.commit()
    lid = cur.lastrowid
    cur.close()
    return lid


def init_db():
    db = sqlite3.connect(DB_PATH)
    db.execute("PRAGMA foreign_keys = ON")
    db.executescript("""
    CREATE TABLE IF NOT EXISTS users (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        name TEXT NOT NULL,
        phone TEXT NOT NULL UNIQUE,
        role TEXT NOT NULL CHECK(role IN ('seller','supplier','admin')),
        city TEXT DEFAULT 'مزارشریف',
        plan TEXT DEFAULT 'free' CHECK(plan IN ('free','pro','gold')),
        seller_code TEXT UNIQUE,
        token TEXT UNIQUE,
        created_at TEXT NOT NULL
    );
    CREATE TABLE IF NOT EXISTS products (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        supplier_id INTEGER NOT NULL REFERENCES users(id),
        name TEXT NOT NULL,
        category TEXT NOT NULL DEFAULT 'عمومی',
        description TEXT DEFAULT '',
        wholesale_price INTEGER NOT NULL CHECK(wholesale_price > 0),
        stock INTEGER NOT NULL DEFAULT 0 CHECK(stock >= 0),
        image_emoji TEXT DEFAULT '📦',
        status TEXT NOT NULL DEFAULT 'active' CHECK(status IN ('active','inactive')),
        created_at TEXT NOT NULL
    );
    CREATE TABLE IF NOT EXISTS seller_products (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        seller_id INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
        product_id INTEGER NOT NULL REFERENCES products(id) ON DELETE CASCADE,
        margin INTEGER NOT NULL DEFAULT 0 CHECK(margin >= 0),
        created_at TEXT NOT NULL,
        UNIQUE(seller_id, product_id)
    );
    CREATE TABLE IF NOT EXISTS orders (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        code TEXT NOT NULL UNIQUE,
        seller_id INTEGER NOT NULL REFERENCES users(id),
        product_id INTEGER NOT NULL REFERENCES products(id),
        supplier_id INTEGER NOT NULL REFERENCES users(id),
        qty INTEGER NOT NULL CHECK(qty > 0),
        wholesale_price INTEGER NOT NULL,
        margin INTEGER NOT NULL,
        sale_unit INTEGER NOT NULL,
        delivery_fee INTEGER NOT NULL,
        total INTEGER NOT NULL,
        seller_earning INTEGER NOT NULL,
        supplier_share INTEGER NOT NULL,
        platform_fee INTEGER NOT NULL,
        customer_name TEXT NOT NULL,
        customer_phone TEXT NOT NULL,
        address TEXT NOT NULL DEFAULT '',
        city TEXT NOT NULL DEFAULT 'مزارشریف',
        status TEXT NOT NULL DEFAULT 'pending',
        created_at TEXT NOT NULL,
        updated_at TEXT NOT NULL
    );
    CREATE TABLE IF NOT EXISTS settlements (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        seller_id INTEGER NOT NULL REFERENCES users(id),
        amount INTEGER NOT NULL CHECK(amount > 0),
        label TEXT DEFAULT '',
        status TEXT NOT NULL DEFAULT 'paid' CHECK(status IN ('paid','pending')),
        created_at TEXT NOT NULL,
        paid_at TEXT
    );
    """)
    db.commit()
    db.close()


def now_iso():
    return datetime.now().isoformat(timespec="seconds")


def err(message, code=400):
    return jsonify({"ok": False, "error": message}), code


def valid_phone(phone):
    return isinstance(phone, str) and re.fullmatch(r"07\d{8}", phone.strip()) is not None


def gen_token():
    return secrets.token_urlsafe(24)


def gen_seller_code(db, name):
    base = re.sub(r"\s+", "", name or "S")[:4] or "S"
    for _ in range(20):
        code = f"{base}{secrets.randbelow(9000) + 1000}"
        row = db.execute("SELECT 1 FROM users WHERE seller_code=?", (code,)).fetchone()
        if not row:
            return code
    return f"S{secrets.randbelow(900000) + 100000}"


def gen_order_code(db):
    row = db.execute("SELECT COALESCE(MAX(id),0)+1 AS n FROM orders").fetchone()
    return f"MZ-{1000 + (row['n'] if row else 1)}"


def current_user():
    token = request.headers.get("X-Token") or request.args.get("token") or ""
    if not token:
        return None
    return q("SELECT * FROM users WHERE token=?", (token,), one=True)


def require_roles(*roles):
    def deco(fn):
        @wraps(fn)
        def wrapper(*a, **kw):
            u = current_user()
            if not u:
                return err("وارد نشده‌اید. لطفاً ابتدا وارد شوید.", 401)
            if roles and u["role"] not in roles:
                return err("دسترسی غیرمجاز.", 403)
            g.user = u
            return fn(*a, **kw)
        return wrapper
    return deco


def plan_limit(plan):
    return FREE_PLAN_LIMIT if plan == "free" else -1  # -1 = unlimited


def public_user(u):
    return {k: u[k] for k in ("id", "name", "phone", "role", "city", "plan", "seller_code", "created_at")}


# ---------------- Auth ----------------
@app.post("/api/auth/register")
def register():
    d = request.get_json(force=True, silent=True) or {}
    name = (d.get("name") or "").strip()
    phone = (d.get("phone") or "").strip()
    role = (d.get("role") or "seller").strip()
    city = (d.get("city") or "مزارشریف").strip()
    if len(name) < 2:
        return err("نام باید حداقل ۲ حرف باشد.")
    if not valid_phone(phone):
        return err("شماره تماس معتبر نیست. مثال: 0701234567")
    if role not in ("seller", "supplier"):
        return err("نقش نامعتبر است.")
    if q("SELECT 1 FROM users WHERE phone=?", (phone,), one=True):
        return err("این شماره قبلاً ثبت شده است. وارد شوید.", 409)
    db = get_db()
    code = gen_seller_code(db, name) if role == "seller" else None
    token = gen_token()
    uid = exec_write(
        "INSERT INTO users(name,phone,role,city,seller_code,token,created_at) VALUES(?,?,?,?,?,?,?)",
        (name, phone, role, city, code, token, now_iso()),
    )
    u = q("SELECT * FROM users WHERE id=?", (uid,), one=True)
    return jsonify({"ok": True, "token": token, "user": public_user(u)})


@app.post("/api/auth/login")
def login():
    d = request.get_json(force=True, silent=True) or {}
    phone = (d.get("phone") or "").strip()
    u = q("SELECT * FROM users WHERE phone=?", (phone,), one=True)
    if not u:
        return err("حسابی با این شماره یافت نشد. ابتدا ثبت‌نام کنید.", 404)
    if not u["token"]:
        token = gen_token()
        exec_write("UPDATE users SET token=? WHERE id=?", (token, u["id"]))
        u["token"] = token
    return jsonify({"ok": True, "token": u["token"], "user": public_user(u)})


@app.get("/api/auth/me")
@require_roles("seller", "supplier", "admin")
def me():
    return jsonify({"ok": True, "user": public_user(g.user)})


@app.get("/api/meta")
def meta():
    return jsonify({"ok": True, "cities": CITIES, "delivery_fee": DELIVERY_FEE,
                    "platform_rate": PLATFORM_RATE, "free_plan_limit": FREE_PLAN_LIMIT})


# ---------------- Public catalog ----------------
@app.get("/api/products")
def products():
    cat = (request.args.get("category") or "").strip()
    search = (request.args.get("q") or "").strip()
    sql = """SELECT p.*, u.name AS supplier_name FROM products p
             JOIN users u ON u.id=p.supplier_id
             WHERE p.status='active' AND p.stock > 0"""
    args = []
    if cat:
        sql += " AND p.category=?"
        args.append(cat)
    if search:
        sql += " AND p.name LIKE ?"
        args.append(f"%{search}%")
    sql += " ORDER BY p.id DESC"
    return jsonify({"ok": True, "products": q(sql, args)})


@app.get("/api/categories")
def categories():
    rows = q("SELECT DISTINCT category FROM products WHERE status='active' ORDER BY category")
    return jsonify({"ok": True, "categories": [r["category"] for r in rows]})


# ---------------- Seller ----------------
@app.get("/api/seller/dashboard")
@require_roles("seller")
def seller_dashboard():
    sid = g.user["id"]
    agg = q("""SELECT
        COALESCE(SUM(CASE WHEN status='delivered' THEN seller_earning ELSE 0 END),0) AS earned,
        COALESCE(SUM(CASE WHEN status IN ('pending','confirmed','shipped') THEN seller_earning ELSE 0 END),0) AS pending_sum,
        COUNT(*) AS orders_count,
        COALESCE(SUM(CASE WHEN status='delivered' THEN 1 ELSE 0 END),0) AS delivered_count
        FROM orders WHERE seller_id=?""", (sid,), one=True)
    nprod = q("SELECT COUNT(*) AS c FROM seller_products WHERE seller_id=?", (sid,), one=True)["c"]
    paid = q("SELECT COALESCE(SUM(amount),0) AS s FROM settlements WHERE seller_id=? AND status='paid'", (sid,), one=True)["s"]
    return jsonify({"ok": True, "dashboard": {
        "earned": agg["earned"], "pending_earnings": agg["pending_sum"],
        "withdrawable": max(agg["earned"] - paid, 0), "paid_out": paid,
        "orders_count": agg["orders_count"], "delivered_count": agg["delivered_count"],
        "products_count": nprod, "plan": g.user["plan"],
        "plan_limit": plan_limit(g.user["plan"]), "seller_code": g.user["seller_code"],
    }})


@app.get("/api/seller/items")
@require_roles("seller")
def seller_items():
    rows = q("""SELECT sp.id, sp.margin, sp.product_id,
        p.name, p.category, p.description, p.wholesale_price, p.stock, p.image_emoji, p.status,
        (p.wholesale_price + sp.margin) AS sale_price
        FROM seller_products sp JOIN products p ON p.id=sp.product_id
        WHERE sp.seller_id=? ORDER BY sp.id DESC""", (g.user["id"],))
    return jsonify({"ok": True, "items": rows})


@app.post("/api/seller/items")
@require_roles("seller")
def seller_add_item():
    d = request.get_json(force=True, silent=True) or {}
    try:
        pid = int(d.get("product_id"))
        margin = int(d.get("margin", 0))
    except (TypeError, ValueError):
        return err("اطلاعات نامعتبر است.")
    if margin < 0:
        return err("سود نمی‌تواند منفی باشد.")
    p = q("SELECT * FROM products WHERE id=? AND status='active'", (pid,), one=True)
    if not p:
        return err("محصول یافت نشد.", 404)
    if q("SELECT 1 FROM seller_products WHERE seller_id=? AND product_id=?", (g.user["id"], pid), one=True):
        return err("این محصول قبلاً در فروشگاه شماست.", 409)
    lim = plan_limit(g.user["plan"])
    if lim >= 0:
        n = q("SELECT COUNT(*) AS c FROM seller_products WHERE seller_id=?", (g.user["id"],), one=True)["c"]
        if n >= lim:
            return err(f"پلان رایگان حداکثر {lim} محصول دارد. برای محصول نامحدود پلان حرفه‌ای بگیرید.", 403)
    exec_write("INSERT INTO seller_products(seller_id,product_id,margin,created_at) VALUES(?,?,?,?)",
               (g.user["id"], pid, margin, now_iso()))
    return jsonify({"ok": True, "message": "محصول به فروشگاه شما اضافه شد.",
                    "sale_price": p["wholesale_price"] + margin})


@app.put("/api/seller/items/<int:item_id>")
@require_roles("seller")
def seller_edit_item(item_id):
    d = request.get_json(force=True, silent=True) or {}
    try:
        margin = int(d.get("margin"))
    except (TypeError, ValueError):
        return err("سود نامعتبر است.")
    if margin < 0:
        return err("سود نمی‌تواند منفی باشد.")
    it = q("SELECT * FROM seller_products WHERE id=? AND seller_id=?", (item_id, g.user["id"]), one=True)
    if not it:
        return err("یافت نشد.", 404)
    exec_write("UPDATE seller_products SET margin=? WHERE id=?", (margin, item_id))
    p = q("SELECT wholesale_price FROM products WHERE id=?", (it["product_id"],), one=True)
    return jsonify({"ok": True, "sale_price": p["wholesale_price"] + margin})


@app.delete("/api/seller/items/<int:item_id>")
@require_roles("seller")
def seller_del_item(item_id):
    it = q("SELECT 1 FROM seller_products WHERE id=? AND seller_id=?", (item_id, g.user["id"]), one=True)
    if not it:
        return err("یافت نشد.", 404)
    exec_write("DELETE FROM seller_products WHERE id=?", (item_id,))
    return jsonify({"ok": True})


@app.get("/api/seller/suggest/<int:product_id>")
@require_roles("seller")
def suggest_margin(product_id):
    p = q("SELECT * FROM products WHERE id=?", (product_id,), one=True)
    if not p:
        return err("محصول یافت نشد.", 404)
    w = p["wholesale_price"]
    def rnd(x):
        return int(round(x / 10.0) * 10)
    tiers = [
        {"label": "فروش سریع ⚡", "desc": "قیمت پایین‌تر، فروش بیشتر", "margin": rnd(w * 0.15)},
        {"label": "متعادل ⭐ (پیشنهاد)", "desc": "بهترین تعادل قیمت و سود", "margin": rnd(w * 0.25)},
        {"label": "حداکثر سود 💰", "desc": "سود بالا، فروش کمتر", "margin": rnd(w * 0.40)},
    ]
    for t in tiers:
        t["sale_price"] = w + t["margin"]
    return jsonify({"ok": True, "wholesale": w, "tiers": tiers})


@app.get("/api/seller/orders")
@require_roles("seller")
def seller_orders():
    rows = q("""SELECT o.*, p.name AS product_name, p.image_emoji FROM orders o
        JOIN products p ON p.id=o.product_id WHERE o.seller_id=? ORDER BY o.id DESC""",
        (g.user["id"],))
    return jsonify({"ok": True, "orders": rows})


@app.get("/api/seller/settlements")
@require_roles("seller")
def seller_settlements():
    rows = q("SELECT * FROM settlements WHERE seller_id=? ORDER BY id DESC", (g.user["id"],))
    earned = q("SELECT COALESCE(SUM(seller_earning),0) AS s FROM orders WHERE seller_id=? AND status='delivered'",
               (g.user["id"],), one=True)["s"]
    paid = q("SELECT COALESCE(SUM(amount),0) AS s FROM settlements WHERE seller_id=? AND status='paid'",
             (g.user["id"],), one=True)["s"]
    return jsonify({"ok": True, "settlements": rows, "earned": earned, "paid": paid,
                    "withdrawable": max(earned - paid, 0)})


@app.get("/api/seller/share")
@require_roles("seller")
def seller_share():
    base = request.host_url.rstrip("/")
    code = g.user["seller_code"]
    url = f"{base}/shop.html?s={code}"
    text = f"🛍️ فروشگاه من: {url}\n✅ پرداخت هنگام تحویل | 🚚 ارسال در مزارشریف"
    return jsonify({"ok": True, "seller_code": code, "shop_url": url,
                    "whatsapp": f"https://wa.me/?text={text}",
                    "telegram": f"https://t.me/share/url?url={url}"})


# ---------------- Supplier ----------------
@app.get("/api/supplier/overview")
@require_roles("supplier")
def supplier_overview():
    sid = g.user["id"]
    nprod = q("SELECT COUNT(*) AS c FROM products WHERE supplier_id=? AND status='active'", (sid,), one=True)["c"]
    agg = q("""SELECT COUNT(*) AS c,
        COALESCE(SUM(CASE WHEN status NOT IN ('cancelled','returned') THEN supplier_share ELSE 0 END),0) AS revenue
        FROM orders WHERE supplier_id=?""", (sid,), one=True)
    low = q("SELECT COUNT(*) AS c FROM products WHERE supplier_id=? AND status='active' AND stock < 5", (sid,), one=True)["c"]
    return jsonify({"ok": True, "overview": {"products": nprod, "orders": agg["c"],
                                             "revenue": agg["revenue"], "low_stock": low}})


@app.get("/api/supplier/products")
@require_roles("supplier")
def supplier_products():
    rows = q("SELECT * FROM products WHERE supplier_id=? ORDER BY id DESC", (g.user["id"],))
    return jsonify({"ok": True, "products": rows})


@app.post("/api/supplier/products")
@require_roles("supplier")
def supplier_add_product():
    d = request.get_json(force=True, silent=True) or {}
    name = (d.get("name") or "").strip()
    try:
        price = int(d.get("wholesale_price", 0))
        stock = int(d.get("stock", 0))
    except (TypeError, ValueError):
        return err("قیمت و موجودی باید عدد باشند.")
    if len(name) < 2:
        return err("نام محصول الزامی است.")
    if price <= 0:
        return err("قیمت عمده باید بیشتر از صفر باشد.")
    if stock < 0:
        return err("موجودی نمی‌تواند منفی باشد.")
    pid = exec_write("""INSERT INTO products(supplier_id,name,category,description,wholesale_price,stock,image_emoji,created_at)
        VALUES(?,?,?,?,?,?,?,?)""", (g.user["id"], name, (d.get("category") or "عمومی").strip() or "عمومی",
        (d.get("description") or "").strip(), price, stock, (d.get("image_emoji") or "📦").strip(), now_iso()))
    return jsonify({"ok": True, "id": pid})


@app.put("/api/supplier/products/<int:pid>")
@require_roles("supplier")
def supplier_edit_product(pid):
    p = q("SELECT * FROM products WHERE id=? AND supplier_id=?", (pid, g.user["id"]), one=True)
    if not p:
        return err("محصول یافت نشد.", 404)
    d = request.get_json(force=True, silent=True) or {}
    fields = {}
    for k in ("name", "category", "description", "image_emoji", "status"):
        if k in d and isinstance(d[k], str):
            fields[k] = d[k].strip()
    for k in ("wholesale_price", "stock"):
        if k in d:
            try:
                fields[k] = int(d[k])
            except (TypeError, ValueError):
                return err(f"مقدار {k} نامعتبر است.")
    if "wholesale_price" in fields and fields["wholesale_price"] <= 0:
        return err("قیمت عمده باید بیشتر از صفر باشد.")
    if "stock" in fields and fields["stock"] < 0:
        return err("موجودی نمی‌تواند منفی باشد.")
    if "status" in fields and fields["status"] not in ("active", "inactive"):
        return err("وضعیت نامعتبر است.")
    if not fields:
        return err("تغییری ارسال نشده است.")
    sets = ", ".join(f"{k}=?" for k in fields)
    exec_write(f"UPDATE products SET {sets} WHERE id=?", (*fields.values(), pid))
    return jsonify({"ok": True})


@app.delete("/api/supplier/products/<int:pid>")
@require_roles("supplier")
def supplier_del_product(pid):
    p = q("SELECT 1 FROM products WHERE id=? AND supplier_id=?", (pid, g.user["id"]), one=True)
    if not p:
        return err("محصول یافت نشد.", 404)
    exec_write("UPDATE products SET status='inactive' WHERE id=?", (pid,))
    return jsonify({"ok": True})


@app.get("/api/supplier/orders")
@require_roles("supplier")
def supplier_orders():
    rows = q("""SELECT o.*, p.name AS product_name, s.name AS seller_name FROM orders o
        JOIN products p ON p.id=o.product_id JOIN users s ON s.id=o.seller_id
        WHERE o.supplier_id=? ORDER BY o.id DESC""", (g.user["id"],))
    return jsonify({"ok": True, "orders": rows})


# ---------------- Public shop + orders ----------------
@app.get("/api/shop/<code>")
def shop(code):
    seller = q("SELECT id, name, seller_code, city FROM users WHERE seller_code=? AND role='seller'",
               (code,), one=True)
    if not seller:
        return err("فروشگاه یافت نشد.", 404)
    items = q("""SELECT sp.product_id, sp.margin, p.name, p.category, p.description,
        p.wholesale_price, p.stock, p.image_emoji, (p.wholesale_price + sp.margin) AS sale_price
        FROM seller_products sp JOIN products p ON p.id=sp.product_id
        WHERE sp.seller_id=? AND p.status='active' AND p.stock > 0 ORDER BY sp.id DESC""",
        (seller["id"],))
    return jsonify({"ok": True, "seller": seller, "items": items, "delivery_fee": DELIVERY_FEE})


@app.post("/api/orders")
def create_order():
    d = request.get_json(force=True, silent=True) or {}
    code = (d.get("seller_code") or "").strip()
    name = (d.get("customer_name") or "").strip()
    phone = (d.get("customer_phone") or "").strip()
    address = (d.get("address") or "").strip()
    city = (d.get("city") or "مزارشریف").strip()
    try:
        pid = int(d.get("product_id"))
        qty = int(d.get("qty", 1))
    except (TypeError, ValueError):
        return err("محصول یا تعداد نامعتبر است.")
    if len(name) < 2:
        return err("نام مشتری الزامی است.")
    if not valid_phone(phone):
        return err("شماره تماس مشتری معتبر نیست. مثال: 0701234567")
    if qty < 1 or qty > 50:
        return err("تعداد باید بین ۱ تا ۵۰ باشد.")
    seller = q("SELECT * FROM users WHERE seller_code=? AND role='seller'", (code,), one=True)
    if not seller:
        return err("فروشنده یافت نشد.", 404)
    sp = q("SELECT * FROM seller_products WHERE seller_id=? AND product_id=?",
           (seller["id"], pid), one=True)
    if not sp:
        return err("این محصول در فروشگاه این فروشنده نیست.", 404)
    p = q("SELECT * FROM products WHERE id=? AND status='active'", (pid,), one=True)
    if not p:
        return err("محصول ناموجود است.", 404)
    if p["stock"] < qty:
        return err(f"موجودی کافی نیست. موجودی فعلی: {p['stock']}", 409)
    w, m = p["wholesale_price"], sp["margin"]
    sale_unit = w + m
    total = sale_unit * qty + DELIVERY_FEE
    db = get_db()
    ocode = gen_order_code(db)
    exec_write("""INSERT INTO orders(code,seller_id,product_id,supplier_id,qty,wholesale_price,margin,
        sale_unit,delivery_fee,total,seller_earning,supplier_share,platform_fee,
        customer_name,customer_phone,address,city,status,created_at,updated_at)
        VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)""",
        (ocode, seller["id"], pid, p["supplier_id"], qty, w, m, sale_unit, DELIVERY_FEE, total,
         m * qty, w * qty, round(w * qty * PLATFORM_RATE),
         name, phone, address, city, "pending", now_iso(), now_iso()))
    exec_write("UPDATE products SET stock=stock-? WHERE id=?", (qty, pid))
    return jsonify({"ok": True, "message": "سفارش شما ثبت شد! پرداخت هنگام تحویل. 🛵",
                    "code": ocode, "total": total})


@app.get("/api/orders/track/<code>")
def track_order(code):
    o = q("""SELECT o.code,o.qty,o.sale_unit,o.delivery_fee,o.total,o.status,o.created_at,o.city,
        p.name AS product_name, p.image_emoji, s.name AS seller_name
        FROM orders o JOIN products p ON p.id=o.product_id JOIN users s ON s.id=o.seller_id
        WHERE o.code=?""", (code.strip(),), one=True)
    if not o:
        return err("سفارشی با این کد یافت نشد.", 404)
    return jsonify({"ok": True, "order": o})


# ---------------- Admin ----------------
@app.get("/api/admin/overview")
@require_roles("admin")
def admin_overview():
    users = q("SELECT role, COUNT(*) AS c FROM users GROUP BY role")
    by_status = q("SELECT status, COUNT(*) AS c, COALESCE(SUM(total),0) AS t FROM orders GROUP BY status")
    gmv = q("SELECT COALESCE(SUM(total),0) AS s FROM orders WHERE status NOT IN ('cancelled','returned')", one=True)["s"]
    plat = q("SELECT COALESCE(SUM(platform_fee),0) AS s FROM orders WHERE status='delivered'", one=True)["s"]
    s_owed = q("SELECT COALESCE(SUM(o.seller_earning),0) AS s FROM orders o WHERE o.status='delivered'", one=True)["s"]
    s_paid = q("SELECT COALESCE(SUM(amount),0) AS s FROM settlements WHERE status='paid'", one=True)["s"]
    nprod = q("SELECT COUNT(*) AS c FROM products WHERE status='active'", one=True)["c"]
    recent = q("""SELECT o.code,o.total,o.status,o.created_at,p.name AS product_name,s.name AS seller_name
        FROM orders o JOIN products p ON p.id=o.product_id JOIN users s ON s.id=o.seller_id
        ORDER BY o.id DESC LIMIT 10""")
    return jsonify({"ok": True, "overview": {
        "users_by_role": {r["role"]: r["c"] for r in users},
        "orders_by_status": {r["status"]: {"count": r["c"], "total": r["t"]} for r in by_status},
        "gmv": gmv, "platform_revenue": plat, "active_products": nprod,
        "seller_owed": max(s_owed - s_paid, 0), "seller_earned_total": s_owed, "seller_paid_total": s_paid,
        "recent_orders": recent}})


@app.get("/api/admin/orders")
@require_roles("admin")
def admin_orders():
    st = (request.args.get("status") or "").strip()
    sql = """SELECT o.*, p.name AS product_name, s.name AS seller_name, s.phone AS seller_phone,
        u.name AS supplier_name FROM orders o
        JOIN products p ON p.id=o.product_id JOIN users s ON s.id=o.seller_id
        JOIN users u ON u.id=o.supplier_id"""
    args = []
    if st:
        sql += " WHERE o.status=?"
        args.append(st)
    sql += " ORDER BY o.id DESC LIMIT 300"
    return jsonify({"ok": True, "orders": q(sql, args)})


@app.put("/api/admin/orders/<int:oid>")
@require_roles("admin")
def admin_set_status(oid):
    o = q("SELECT * FROM orders WHERE id=?", (oid,), one=True)
    if not o:
        return err("سفارش یافت نشد.", 404)
    d = request.get_json(force=True, silent=True) or {}
    new = (d.get("status") or "").strip()
    if new not in ORDER_FLOW.get(o["status"], []):
        return err(f"تغییر وضعیت از {o['status']} به {new} مجاز نیست.")
    exec_write("UPDATE orders SET status=?, updated_at=? WHERE id=?", (new, now_iso(), oid))
    if new in ("cancelled", "returned"):
        exec_write("UPDATE products SET stock=stock+? WHERE id=?", (o["qty"], o["product_id"]))
    return jsonify({"ok": True})


@app.get("/api/admin/users")
@require_roles("admin")
def admin_users():
    rows = q("""SELECT u.*, (SELECT COUNT(*) FROM orders o WHERE o.seller_id=u.id) AS orders_count,
        (SELECT COALESCE(SUM(o2.seller_earning),0) FROM orders o2 WHERE o2.seller_id=u.id AND o2.status='delivered') AS earned
        FROM users u ORDER BY u.id DESC""")
    return jsonify({"ok": True, "users": [public_user(r) | {"orders_count": r["orders_count"], "earned": r["earned"]} for r in rows]})


@app.put("/api/admin/users/<int:uid>/plan")
@require_roles("admin")
def admin_set_plan(uid):
    d = request.get_json(force=True, silent=True) or {}
    plan = (d.get("plan") or "").strip()
    if plan not in ("free", "pro", "gold"):
        return err("پلان نامعتبر است.")
    u = q("SELECT 1 FROM users WHERE id=?", (uid,), one=True)
    if not u:
        return err("کاربر یافت نشد.", 404)
    exec_write("UPDATE users SET plan=? WHERE id=?", (plan, uid))
    return jsonify({"ok": True})


@app.get("/api/admin/settlements")
@require_roles("admin")
def admin_settlements():
    rows = q("""SELECT u.id, u.name, u.phone, u.seller_code,
        COALESCE((SELECT SUM(o.seller_earning) FROM orders o WHERE o.seller_id=u.id AND o.status='delivered'),0) AS earned,
        COALESCE((SELECT SUM(s2.amount) FROM settlements s2 WHERE s2.seller_id=u.id AND s2.status='paid'),0) AS paid
        FROM users u WHERE u.role='seller' ORDER BY u.id""")
    out = []
    for r in rows:
        out.append({**r, "withdrawable": max(r["earned"] - r["paid"], 0)})
    log = q("""SELECT s.*, u.name AS seller_name FROM settlements s
        JOIN users u ON u.id=s.seller_id ORDER BY s.id DESC LIMIT 50""")
    return jsonify({"ok": True, "sellers": out, "log": log})


@app.post("/api/admin/settlements")
@require_roles("admin")
def admin_pay():
    d = request.get_json(force=True, silent=True) or {}
    try:
        sid = int(d.get("seller_id"))
        amount = int(d.get("amount"))
    except (TypeError, ValueError):
        return err("اطلاعات نامعتبر است.")
    if amount <= 0:
        return err("مبلغ باید مثبت باشد.")
    seller = q("SELECT 1 FROM users WHERE id=? AND role='seller'", (sid,), one=True)
    if not seller:
        return err("فروشنده یافت نشد.", 404)
    exec_write("INSERT INTO settlements(seller_id,amount,label,status,created_at,paid_at) VALUES(?,?,?,?,?,?)",
               (sid, amount, (d.get("label") or "تسویه دستی").strip(), "paid", now_iso(), now_iso()))
    return jsonify({"ok": True, "message": "تسویه ثبت شد."})


# ---------------- Static pages ----------------
@app.get("/")
def root():
    return send_from_directory(STATIC_DIR, "index.html")


@app.get("/<path:fname>")
def static_files(fname):
    if fname.startswith("api/"):
        return err("یافت نشد.", 404)
    fpath = os.path.join(STATIC_DIR, fname)
    if os.path.isfile(fpath):
        return send_from_directory(STATIC_DIR, fname)
    return send_from_directory(STATIC_DIR, "index.html")


if __name__ == "__main__":
    init_db()
    print("DB:", DB_PATH)
    print("Serving on http://0.0.0.0:5000")
    app.run(host="0.0.0.0", port=5000, debug=False)
