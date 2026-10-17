"""Ölçüm kaydını (olcumler/*.csv) oyunun hız mantığından geçir: eski (sabit 1,5 cm ölü bölge) ile
yeni (öne taraf yan eğimle esneyen ölü bölge, en fazla 3 cm + 0,25 sn yumuşatma). egim sütunu yoksa (eski kayıt) 0 sayılır.
Python tarafı taklit: egim ve one nötre göre, EMA 0,5. Oyun: katkı = OluBolge(one, esik)*150*0,9.
Kullanım: python analiz/hiz_simulasyon.py olcumler/yan_20261017_161252.csv"""
import csv, math, sys, statistics as st

rows = list(csv.DictReader(open(sys.argv[1], encoding="utf-8")))
for r in rows:
    for a in ("t", "dz", "evrede_sn"):
        r[a] = float(r[a])
    r["evre"] = int(r["evre"]); r["egim"] = float(r.get("egim") or 0)
ilk = min(r["evre"] for r in rows if r["etiket"] == "dik")
notr = [r for r in rows if r["evre"] == ilk and 1.0 <= r["evrede_sn"] <= 3.0]
ne, nd = st.mean(r["egim"] for r in notr), st.mean(r["dz"] for r in notr)

def olu(x, e): return 0.0 if abs(x) <= e else x - math.copysign(e, x)

de = do = None
duz = None
onceki_t = None
sonuc = {}
for r in rows:
    if r["evre"] < ilk:
        continue
    e, o = r["egim"] - ne, -(r["dz"] - nd)
    de = e if de is None else 0.5 * e + 0.5 * de
    do = o if do is None else 0.5 * o + 0.5 * do
    dt = 1 / 30 if onceki_t is None else max(r["t"] - onceki_t, 1e-3)
    onceki_t = r["t"]
    a = 1 - math.exp(-dt / 0.25)
    duz = do if duz is None else duz + (do - duz) * a
    eski = olu(do, 0.015) * 150 * 0.9
    ileri = min(0.015 + 0.001 * abs(de), 0.03)  # yalnız öne taraf genişler, en fazla 3 cm
    yeni = (duz - ileri if duz > ileri else (duz + 0.015 if duz < -0.015 else 0.0)) * 150 * 0.9
    sonuc.setdefault((r["evre"], r["etiket"]), []).append((eski, yeni))

print("evre etiket  eski: %kare hiz degisti / ort |dv| m/sn   yeni: %kare / ort |dv|")
for (ev, et), v in sorted(sonuc.items()):
    if et == "hazir":
        continue
    def oz(i):
        x = [p[i] for p in v]
        return 100 * sum(1 for q in x if q != 0) / len(x), st.mean(abs(q) for q in x), max(abs(q) for q in x)
    a1, b1, c1 = oz(0); a2, b2, c2 = oz(1)
    print(f"{ev:4d} {et:6s}  {a1:5.0f}% {b1:5.2f} (max {c1:4.2f})     {a2:5.0f}% {b2:5.2f} (max {c2:4.2f})")
