#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Seed demo data: users, products, seller picks, orders."""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from app import init_db, DB_PATH, DELIVERY_FEE, PLATFORM_RATE
import sqlite3
from datetime import datetime, timedelta

PRODUCTS = [
    # (name, category, desc, wholesale, stock, emoji)
    ("گوشی سامسونگ گلکسی A15 (128GB)", "موبایل و لوازم جانبی", "دو سیم‌کارت، گارانتی شرکتی", 18500, 12, "📱"),
    ("هنذفری بلوتوثی AirPro", "موبایل و لوازم جانبی", "نویزکنسلینگ، ۳۶ ساعت شارژ", 1450, 60, "🎧"),
    ("چارجر سریع 25W سامسونگ", "موبایل و لوازم جانبی", "اصلی با کابل تایپ‌سی", 650, 120, "🔌"),
    ("کاور چرمی آیفون/سامسونگ", "موبایل و لوازم جانبی", "چرم طبیعی، رنگ‌های متنوع", 320, 200, "📲"),
    ("پاوربانک 20000 انکر", "موبایل و لوازم جانبی", "شارژ سریع دوطرفه", 2100, 45, "🔋"),
    ("پیراهن مردانه کتان", "پوشاک", "کتان ترک، سایز M تا XXL", 750, 80, "👕"),
    ("شلوار جین مردانه", "پوشاک", "جین ضخیم، رنگ ثابت", 950, 70, "👖"),
    ("حجاب/شال حریر مجلسی", "پوشاک", "حریر نرم، ۱۲ رنگ", 280, 150, "🧕"),
    ("کرتی زمستانی پشمی", "پوشاک", "گرم و سبک، مناسب زمستان بلخ", 1600, 40, "🧥"),
    ("کفش اسپرت نایک", "پوشاک", "اورجینال، سایز ۴۰ تا ۴۵", 2300, 35, "👟"),
    ("ست آرایشی ۱۲ قلمی", "آرایشی و بهداشتی", "برس حرفه‌ای + کیف", 880, 55, "💄"),
    ("کرم ضدآفتاب SPF50", "آرایشی و بهداشتی", "مناسب پوست حساس", 420, 90, "🧴"),
    ("عطر عربی 100ml", "آرایشی و بهداشتی", "ماندگاری ۱۲ ساعته", 1350, 48, "🌸"),
    ("سشوار حرفه‌ای 2200W", "آرایشی و بهداشتی", "دو سرعته + باد سرد", 1750, 30, "💨"),
    ("قابلمه گرانیتی ۵ پارچه", "لوازم منزل", "نچسب، دسته نسوز", 2900, 25, "🍳"),
    ("چای‌جوش برقی 1.8L", "لوازم منزل", "استیل ضدزنگ، خاموشی خودکار", 1250, 42, "🫖"),
    ("جاروبرقی روباتیک", "لوازم منزل", "شارژی، کنترل با موبایل", 8500, 10, "🤖"),
    ("ست ملافه ۴ نفره", "لوازم منزل", "نخ پنبه، طرح‌های شیک", 1100, 60, "🛏️"),
    ("بخاری گازی کم‌مصرف", "لوازم منزل", "مناسب اتاق تا ۴۰ متر", 3400, 18, "🔥"),
    ("کالسکه نوزاد تاشو", "کودک و نوزاد", "سبک، چرخ قفل‌دار", 4200, 14, "🧸"),
    ("ست لباس نوزادی ۵ تکه", "کودک و نوزاد", "نخ ۱۰۰٪، صفر تا ۱۲ ماه", 680, 75, "👶"),
    ("شیرخشک نان ۸۰۰g", "کودک و نوزاد", "تاریخ جدید", 980, 50, "🍼"),
    ("اسپیکر بلوتوثی JBL", "الکترونیک", "ضدآب، ۲۰ ساعت پخش", 2600, 28, "🔊"),
    ("ساعت هوشمند FitPro", "الکترونیک", "ضربان‌سنج + قدم‌شمار", 1950, 38, "⌚"),
]

SUPPLIERS = [
    ("عمده‌فروشی بلخ", "0700000002"),
    ("تجارت شمال", "0700000004"),
]

SELLERS = [
    ("احمد محمدی", "0700000003", "احمد1001"),
    ("فاطمه احمدی", "0700000005", "فاطمه1002"),
]


def main():
    init_db()
    db = sqlite3.connect(DB_PATH)
    db.row_factory = sqlite3.Row
    now = datetime.now().isoformat(timespec="seconds")

    if db.execute("SELECT 1 FROM users LIMIT 1").fetchone():
        print("DB already seeded. Delete marketplace.db to reseed.")
        return

    # admin
    db.execute("INSERT INTO users(name,phone,role,city,token,created_at) VALUES(?,?,?,?,?,?)",
               ("مدیر سیستم", "0700000001", "admin", "مزارشریف", "demo-admin-token", now))
    sup_ids = []
    for name, phone in SUPPLIERS:
        cur = db.execute("INSERT INTO users(name,phone,role,city,token,created_at) VALUES(?,?,?,?,?,?)",
                         (name, phone, "supplier", "مزارشریف", f"demo-token-{phone}", now))
        sup_ids.append(cur.lastrowid)
    sel_ids = []
    for name, phone, code in SELLERS:
        cur = db.execute("INSERT INTO users(name,phone,role,city,seller_code,token,created_at) VALUES(?,?,?,?,?,?,?)",
                         (name, phone, "seller", "مزارشریف", code, f"demo-token-{phone}", now))
        sel_ids.append(cur.lastrowid)

    # products (split between suppliers)
    pids = []
    for i, (name, cat, desc, price, stock, emoji) in enumerate(PRODUCTS):
        sup = sup_ids[i % len(sup_ids)]
        cur = db.execute("""INSERT INTO products(supplier_id,name,category,description,wholesale_price,stock,image_emoji,created_at)
            VALUES(?,?,?,?,?,?,?,?)""", (sup, name, cat, desc, price, stock, emoji, now))
        pids.append((cur.lastrowid, price, sup))

    # seller picks with margins (~25%)
    import random
    random.seed(7)
    for sid in sel_ids:
        picks = random.sample(pids, 8)
        for pid, price, _sup in picks:
            margin = int(round(price * 0.25 / 10) * 10)
            db.execute("INSERT INTO seller_products(seller_id,product_id,margin,created_at) VALUES(?,?,?,?)",
                       (sid, pid, margin, now))

    # demo orders across statuses and past days
    customers = [("علی رضایی", "0701111111", "کارته صلح، کوچه ۳"), ("مریم کریمی", "0702222222", "شهر نو، جاده نادرپشتون"),
                 ("حسین نوری", "0703333333", "دشت شور"), ("زهرا حسینی", "0704444444", "کارته آریانا"),
                 ("محمد عظیمی", "0705555555", "پل امام بکری"), ("سارا رحیمی", "0706666666", "خیرخانه مزار")]
    statuses = ["delivered", "delivered", "delivered", "shipped", "confirmed", "pending", "pending", "cancelled"]
    n = 0
    for i, st in enumerate(statuses):
        sid = sel_ids[i % len(sel_ids)]
        row = db.execute("SELECT product_id, margin FROM seller_products WHERE seller_id=? ORDER BY RANDOM() LIMIT 1", (sid,)).fetchone()
        p = db.execute("SELECT * FROM products WHERE id=?", (row["product_id"],)).fetchone()
        qty = 1 if p["wholesale_price"] > 3000 else (2 if i % 3 == 0 else 1)
        w, m = p["wholesale_price"], row["margin"]
        sale_unit = w + m
        total = sale_unit * qty + DELIVERY_FEE
        cname, cphone, addr = customers[i % len(customers)]
        created = (datetime.now() - timedelta(days=len(statuses) - i)).isoformat(timespec="seconds")
        n += 1
        db.execute("""INSERT INTO orders(code,seller_id,product_id,supplier_id,qty,wholesale_price,margin,sale_unit,
            delivery_fee,total,seller_earning,supplier_share,platform_fee,customer_name,customer_phone,address,
            city,status,created_at,updated_at) VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)""",
            (f"MZ-{1000+n}", sid, p["id"], p["supplier_id"], qty, w, m, sale_unit, DELIVERY_FEE, total,
             m*qty, w*qty, round(w*qty*PLATFORM_RATE), cname, cphone, addr, "مزارشریف", st, created, created))
        if st not in ("cancelled", "returned"):
            db.execute("UPDATE products SET stock=stock-? WHERE id=?", (qty, p["id"]))

    # one paid settlement for first seller
    earned = db.execute("SELECT COALESCE(SUM(seller_earning),0) FROM orders WHERE seller_id=? AND status='delivered'", (sel_ids[0],)).fetchone()[0]
    if earned and earned > 100:
        part = int(earned // 2)
        db.execute("INSERT INTO settlements(seller_id,amount,label,status,created_at,paid_at) VALUES(?,?,?,?,?,?)",
                   (sel_ids[0], part, "تسویه هفته اول", "paid", now, now))

    db.commit()
    db.close()
    print("Seeded OK:", DB_PATH)
    print("Demo logins — admin: 0700000001 | supplier: 0700000002 | sellers: 0700000003 / 0700000005")


if __name__ == "__main__":
    main()
