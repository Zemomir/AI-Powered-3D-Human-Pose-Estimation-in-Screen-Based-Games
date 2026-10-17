"""tam_*.csv (egilme_olcum.py --tam) üzerinde öne-geri göstergesi adaylarını karşılaştırır.

Her aday için nötr (ilk "dik" evresinin 1-3. sn'si) çıkarılır, işaret "öne = pozitif" olacak
şekilde öne evresinden ayarlanır, sonra şu sayılar tek birime (öne-geri yarı farkı = S) bölünür:
  sızma sag/sol : yalnız yana eğilmedeki sapma / S      (0'a yakın iyi)
  viraj sag/sol : (yana+öne) değeri / öne değeri         (1'e yakın iyi)
  gürültü       : dik evrelerde kare-kare std / S        (küçük iyi)
  kayma         : dik evre ortalamalarının yayılımı / S  (küçük iyi)
Her evrenin ilk GECIS sn'si atılır (kişi henüz yerleşmedi).

Kullanım: python analiz/aday_karsilastir.py olcumler/tam_YYYYmmdd_HHMMSS.csv
"""
import csv
import math
import statistics as st
import sys

GECIS = 1.5
MIN_GOR = 0.5


def f(x):
    try:
        return float(x)
    except (TypeError, ValueError):
        return None


def g(r, i):  # görüntü noktası, piksel
    return f(r[f"g{i}_x"]) * f(r["gen"]), f(r[f"g{i}_y"]) * f(r["yuk"])


def w(r, i):
    return f(r[f"w{i}_x"]), f(r[f"w{i}_y"]), f(r[f"w{i}_z"])


def gor(r, *idx):
    return all((f(r[f"g{i}_v"]) or 0) >= MIN_GOR for i in idx)


def uz(a, b):
    return math.hypot(a[0] - b[0], a[1] - b[1])


def orta(a, b):
    return tuple((x + y) / 2 for x, y in zip(a, b))


def eksi(a, b):
    return tuple(x - y for x, y in zip(a, b))


def nokta(a, b):
    return sum(x * y for x, y in zip(a, b))


def capraz(a, b):
    return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


def birim(a):
    n = math.sqrt(nokta(a, a))
    return tuple(x / n for x in a)


def adaylar(r, eksen):
    d = {}
    sm_w, hm_w = orta(w(r, 11), w(r, 12)), orta(w(r, 23), w(r, 24))
    d["z (bugunku)"] = sm_w[2] - hm_w[2]
    omuz_px, kalca_px = uz(g(r, 11), g(r, 12)), uz(g(r, 23), g(r, 24))
    d["omuz gen."] = omuz_px
    d["omuz/kalca"] = omuz_px / kalca_px if kalca_px > 1 else None
    if gor(r, 7, 8):
        d["kulak gen./kalca"] = uz(g(r, 7), g(r, 8)) / kalca_px if kalca_px > 1 else None
    else:
        d["kulak gen./kalca"] = None
    d["goz dis/kalca"] = uz(g(r, 2), g(r, 5)) / kalca_px if kalca_px > 1 else None
    if eksen is not None:  # gövde vektörü, nötrdeki gövde eksen takımında açı (derece)
        x, y, z = eksen
        t = eksi(sm_w, hm_w)
        d["govde acisi (eksen)"] = math.degrees(math.atan2(nokta(t, z), nokta(t, y)))
    if gor(r, 27, 28):
        am_w = orta(w(r, 27), w(r, 28))
        d["z ayak bilegine gore"] = sm_w[2] - am_w[2]
    else:
        d["z ayak bilegine gore"] = None
    return d


def main(yol):
    rows = list(csv.DictReader(open(yol, encoding="utf-8")))
    for r in rows:
        r["evre"] = int(r["evre"])
        r["evrede_sn"] = f(r["evrede_sn"])
    ilk = min(r["evre"] for r in rows if r["etiket"] == "dik")
    notr_satir = [r for r in rows if r["evre"] == ilk and 1.0 <= r["evrede_sn"] <= 3.0]
    # Nötr gövde eksen takımı: x = sağ kalça - sol kalça, y = gövde (kalça->omuz), z = x × y
    ort = lambda i: tuple(st.mean(w(r, i)[k] for r in notr_satir) for k in range(3))
    xh = birim(eksi(ort(24), ort(23)))
    yv = eksi(orta(ort(11), ort(12)), orta(ort(23), ort(24)))
    yh = birim(eksi(yv, tuple(nokta(yv, xh) * c for c in xh)))
    zh = capraz(xh, yh)
    eksen = (xh, yh, zh)

    deger = [(r, adaylar(r, eksen)) for r in rows]
    adlar = list(deger[0][1])
    ayak_gor = sum(1 for r in rows if gor(r, 27, 28)) / len(rows)
    print(f"{yol}: {len(rows)} kare, ayak bilekleri gorunur %{100 * ayak_gor:.0f}")
    print(f"{'aday':22s} {'S':>8s} {'sizma sag':>9s} {'sizma sol':>9s} {'viraj sag':>9s} {'viraj sol':>9s} {'gurultu':>8s} {'kayma':>7s}")
    for ad in adlar:
        def ev(etk):
            return [(r, d[ad]) for r, d in deger if r["etiket"] == etk and r["evrede_sn"] >= GECIS and d[ad] is not None]
        n = [d[ad] for r, d in deger if r in notr_satir and d[ad] is not None]
        if len(n) < 10:
            print(f"{ad:22s} yetersiz nötr örnek ({len(n)})")
            continue
        n0 = st.mean(n)
        m = {etk: [v - n0 for _, v in ev(etk)] for etk in ("one", "geri", "sag", "sol", "sag_one", "sol_one", "dik")}
        if not m["one"] or not m["geri"]:
            print(f"{ad:22s} öne/geri örneği yok")
            continue
        isaret = 1 if st.mean(m["one"]) > st.mean(m["geri"]) else -1
        m = {k: [isaret * v for v in vs] for k, vs in m.items()}
        S = (st.mean(m["one"]) - st.mean(m["geri"])) / 2
        one_ort = st.mean(m["one"])
        oz = lambda k: st.mean(m[k]) / S if m[k] else float("nan")
        vr = lambda k: st.mean(m[k]) / one_ort if m[k] else float("nan")
        # gürültü: her dik evresi içindeki std'lerin ortalaması; kayma: dik evre ortalamalarının std'si
        dik_ev = {}
        for r, d in deger:
            if r["etiket"] == "dik" and r["evrede_sn"] >= GECIS and d[ad] is not None:
                dik_ev.setdefault(r["evre"], []).append(isaret * (d[ad] - n0))
        gur = st.mean(st.pstdev(v) for v in dik_ev.values() if len(v) > 5) / S
        kay = st.pstdev([st.mean(v) for v in dik_ev.values() if len(v) > 5]) / S
        print(f"{ad:22s} {S:8.4f} {oz('sag'):+9.2f} {oz('sol'):+9.2f} {vr('sag_one'):9.2f} {vr('sol_one'):9.2f} {gur:8.2f} {kay:7.2f}")


if __name__ == "__main__":
    main(sys.argv[1])
