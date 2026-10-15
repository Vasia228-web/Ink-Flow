"""
SpacePaint · планета «Сфера» — еталонна математика комірок на кулі, що крутиться.

Ідея: кожна комірка — плаский квадрат, що лежить на поверхні кулі (дотичний до неї).
Ми дивимося на кулю «здалеку» (ортографічна проєкція), тож:
  - у центрі кулі комірка майже рівна й повного розміру;
  - ближче до краю вона стискається в напрямку до центру і нахиляється — «загинається» за кулю;
  - за краєм (на звороті) комірку не видно.
Комірки розкидані по кулі хаотично, але детерміновано (той самий seed — те саме розташування).

Цей файл — еталон для Unity: перенеси функції як є і порівняй числа в тестах (див. main).
Координати екрана тут з віссю Y ВГОРУ (як у UGUI), центр кулі = (0, 0), радіус = R.
"""
import math, random

def layout(n, seed=1, tilt_max=18.0):
    """Розкидає n комірок по кулі хаотично, але детерміновано (той самий seed — те саме розташування).
    Довготи — рівномірно навколо кулі з тремтінням; широти — випадкові в поясі ±lat_max;
    psi — випадковий нахил комірки на поверхні ±18°. Комірки не перекриваються.
    Повертає список словників: lat, lon, psi (градуси) і size (сторона комірки в частках радіуса)."""
    lat_max = min(62.0, 36.0 + max(0, n - 8) * 1.6)
    size = slot_size(n, 1.0)
    for shrink in range(12):
        need = size * math.sqrt(2) * 1.08           # мінімальна кутова відстань між центрами (радіани)
        for attempt in range(30):
            rnd = random.Random(seed * 7919 + n * 31 + attempt * 104729 + shrink)
            out = _try(n, rnd, lat_max, need, tilt_max)
            if out:
                for sl in out:
                    sl["size"] = size
                return out
        size *= 0.95
    raise RuntimeError("не вдалося розкласти комірки")

def _vec(lat, lon):
    p, l = math.radians(lat), math.radians(lon)
    return (math.cos(p) * math.sin(l), math.sin(p), math.cos(p) * math.cos(l))

def _try(n, rnd, lat_max, need, tilt_max):
    step = 360.0 / n
    out, V = [], []
    smax = math.sin(math.radians(lat_max))
    for i in range(n):
        for _ in range(200):
            lon = (i * step + rnd.uniform(-0.35, 0.35) * step) % 360.0
            lat = math.degrees(math.asin(rnd.uniform(-smax, smax)))   # рівномірно за площею
            v = _vec(lat, lon)
            if all(math.acos(max(-1.0, min(1.0, sum(a * b for a, b in zip(v, w))))) >= need for w in V):
                out.append({"lat": lat, "lon": lon, "psi": rnd.uniform(-tilt_max, tilt_max)})
                V.append(v)
                break
        else:
            return None
    return out

def slot_size(n, R):
    """Сторона комірки в пікселях макета: що менше комірок, то вони більші."""
    if n <= 4:
        return R * 0.70
    if n <= 6:
        return R * 0.63
    return R * min(0.57, 1.55 / n ** 0.5)

def min_gap_ok(slots, n, margin=1.079):
    """Чи не перекриваються комірки: кутова відстань між центрами ≥ діагональ комірки × margin."""
    size = slots[0]["size"]             # у радіанах дуги (R = 1)
    need = size * math.sqrt(2) * margin
    def vec(sl):
        p, l = math.radians(sl["lat"]), math.radians(sl["lon"])
        return (math.cos(p) * math.sin(l), math.sin(p), math.cos(p) * math.cos(l))
    V = [vec(sl) for sl in slots]
    for i in range(n):
        for j in range(i + 1, n):
            d = math.acos(max(-1.0, min(1.0, sum(a * b for a, b in zip(V[i], V[j])))))
            if d < need:
                return False
    return True

def _tilt(v, a):
    x, y, z = v
    ca, sa = math.cos(a), math.sin(a)
    return (x, y * ca - z * sa, y * sa + z * ca)

def project(slot, rot_deg, axis_tilt_deg=16.0):
    """Позиція й форма комірки на екрані для поточного повороту кулі rot_deg.
    Повертає: x, y (у частках радіуса, Y угору), depth (−1…1, >0 — передній бік),
    m = (m00, m01, m10, m11) — матриця 2×2, яку треба застосувати до квадрата комірки
    (стовпці — куди переходять осі X і Y комірки)."""
    phi = math.radians(slot["lat"])
    lam = math.radians(slot["lon"] - rot_deg)
    psi = math.radians(slot["psi"])
    a = math.radians(axis_tilt_deg)
    P = (math.cos(phi) * math.sin(lam), math.sin(phi), math.cos(phi) * math.cos(lam))
    E = (math.cos(lam), 0.0, -math.sin(lam))                                         # на схід
    N = (-math.sin(phi) * math.sin(lam), math.cos(phi), -math.sin(phi) * math.cos(lam))  # на північ
    P, E, N = _tilt(P, a), _tilt(E, a), _tilt(N, a)
    ax = tuple(math.cos(psi) * e + math.sin(psi) * nn for e, nn in zip(E, N))   # вісь X комірки
    ay = tuple(-math.sin(psi) * e + math.cos(psi) * nn for e, nn in zip(E, N))  # вісь Y комірки
    return {"x": P[0], "y": P[1], "depth": P[2], "m": (ax[0], ay[0], ax[1], ay[1])}

def alpha(depth, hide=0.05, full=0.30):
    """Прозорість: на звороті 0, біля краю плавно з'являється, ближче до центру 1."""
    return max(0.0, min(1.0, (depth - hide) / (full - hide)))

def shade(depth):
    """Наскільки затемнити комірку біля краю (0 — не темнити)."""
    return 0.35 * (1.0 - max(0.0, depth))

def pip_angle(slot, rot_deg):
    """Кут мітки прогресу на кільці навколо планети, у градусах екрана (0 — праворуч, 90 — угору).
    Передній бік кулі завжди вгорі кільця."""
    return 90.0 - ((slot["lon"] - rot_deg + 180.0) % 360.0 - 180.0)

def svd2(m):
    """Розклад 2×2 матриці на поворот·масштаб·поворот: M = R(t1)·S(sx, sy)·R(t2).
    Потрібен, щоб зібрати форму комірки з двох вкладених RectTransform у UGUI:
    зовнішній — rotation t1 і scale (sx, sy), внутрішній — rotation t2."""
    a, b, c, d = m
    E = (a + d) / 2; F = (a - d) / 2; G = (c + b) / 2; H = (c - b) / 2
    Q = math.hypot(E, H); Rr = math.hypot(F, G)
    sx, sy = Q + Rr, Q - Rr
    a1 = math.atan2(G, F); a2 = math.atan2(H, E)
    t2 = (a2 - a1) / 2; t1 = (a2 + a1) / 2
    return math.degrees(t1), sx, sy, math.degrees(t2)

if __name__ == "__main__":
    # Контрольні числа для тестів у Unity
    for n, seed in [(4, 1), (8, 3)]:
        L = layout(n, seed)
        print(f"n={n} seed={seed}")
        for i, s in enumerate(L):
            p = project(s, rot_deg=30)
            print(f"  #{i} lat={s['lat']:7.2f} lon={s['lon']:7.2f} psi={s['psi']:6.2f} | x={p['x']:.3f} y={p['y']:.3f} depth={p['depth']:.3f} "
                  f"alpha={alpha(p['depth']):.2f} m=({', '.join(f'{v:.3f}' for v in p['m'])}) svd={tuple(round(v, 2) for v in svd2(p['m']))}")
