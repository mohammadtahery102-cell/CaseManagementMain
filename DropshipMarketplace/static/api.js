/* Shared helpers: API, auth, formatting, toast */
const store = {
  get token() { return localStorage.getItem('dm_token') || ''; },
  get user() { try { return JSON.parse(localStorage.getItem('dm_user') || 'null'); } catch { return null; } },
  set(token, user) { localStorage.setItem('dm_token', token); localStorage.setItem('dm_user', JSON.stringify(user)); },
  clear() { localStorage.removeItem('dm_token'); localStorage.removeItem('dm_user'); }
};

async function api(path, opts = {}) {
  const headers = Object.assign({'Content-Type': 'application/json'}, opts.headers || {});
  if (store.token) headers['X-Token'] = store.token;
  const res = await fetch(path, Object.assign({}, opts, {headers}));
  let data = null;
  try { data = await res.json(); } catch { data = {ok: false, error: 'خطای ارتباط با سرور'}; }
  if (!res.ok && data && data.ok === undefined) data.ok = false;
  return data;
}
const apiGet = (p) => api(p);
const apiPost = (p, b) => api(p, {method: 'POST', body: JSON.stringify(b || {})});
const apiPut = (p, b) => api(p, {method: 'PUT', body: JSON.stringify(b || {})});
const apiDel = (p) => api(p, {method: 'DELETE'});

function fmt(n) {
  n = Number(n || 0);
  return n.toLocaleString('en-US') + ' ؋';
}
function faDate(iso) {
  if (!iso) return '—';
  try { return new Date(iso).toLocaleDateString('fa-AF', {year: 'numeric', month: 'short', day: 'numeric'}); }
  catch { return iso; }
}
function toast(msg, isErr = false) {
  let el = document.getElementById('toast');
  if (!el) { el = document.createElement('div'); el.id = 'toast'; el.className = 'toast'; document.body.appendChild(el); }
  el.textContent = msg;
  el.classList.toggle('err', !!isErr);
  el.classList.add('show');
  clearTimeout(el._t);
  el._t = setTimeout(() => el.classList.remove('show'), 3200);
}
const STATUS_FA = {pending: 'در انتظار', confirmed: 'تأیید شده', shipped: 'ارسال شده', delivered: 'تحویل شده', cancelled: 'لغو شده', returned: 'مرجوعی'};
function statusBadge(st) { return `<span class="status st-${st}">${STATUS_FA[st] || st}</span>`; }
const PLAN_FA = {free: 'رایگان', pro: 'حرفه‌ای', gold: 'طلایی'};

function guard(roles) {
  const u = store.user;
  if (!u || !store.token) { location.href = 'auth.html'; return null; }
  if (roles && !roles.includes(u.role)) {
    toast('دسترسی غیرمجاز', true);
    location.href = u.role === 'seller' ? 'seller.html' : u.role === 'supplier' ? 'supplier.html' : 'admin.html';
    return null;
  }
  return u;
}
function logout() { store.clear(); location.href = 'index.html'; }
function renderTopbar(activeUser) {
  const u = activeUser || store.user;
  const nav = document.getElementById('nav-links');
  if (!nav) return;
  if (!u) {
    nav.innerHTML = `<a class="btn btn-ghost btn-sm" href="auth.html">ورود / ثبت‌نام</a>
      <a class="btn btn-primary btn-sm" href="auth.html?role=seller">🚀 فروشنده شو</a>`;
  } else {
    const panel = u.role === 'seller' ? 'seller.html' : u.role === 'supplier' ? 'supplier.html' : 'admin.html';
    nav.innerHTML = `<span class="muted">👋 ${escapeHtml(u.name)}</span>
      <a class="btn btn-ghost btn-sm" href="${panel}">پنل من</a>
      <button class="btn btn-ghost btn-sm" onclick="logout()">خروج</button>`;
  }
}
function escapeHtml(s) {
  return String(s == null ? '' : s).replace(/[&<>"']/g, c => ({'&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'}[c]));
}
function copyText(t, msg) {
  navigator.clipboard.writeText(t).then(() => toast(msg || 'کپی شد ✓')).catch(() => {
    const ta = document.createElement('textarea'); ta.value = t; document.body.appendChild(ta);
    ta.select(); document.execCommand('copy'); ta.remove(); toast(msg || 'کپی شد ✓');
  });
}
