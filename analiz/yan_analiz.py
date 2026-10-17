"""yan_*.csv: her evrede (ilk 1 sn geçiş hariç) egim, dz, donme'nin nötre göre ortalaması ve sapması."""
import csv, sys, statistics as st

satirlar = list(csv.DictReader(open(sys.argv[1], encoding="utf-8")))
for s in satirlar:
    for a in ("egim", "dz", "donme", "evrede_sn"):
        s[a] = float(s[a])
    s["evre"] = int(s["evre"])

ilk_dik = min(s["evre"] for s in satirlar if s["etiket"] == "dik")
notr = [s for s in satirlar if s["evre"] == ilk_dik and 1.0 <= s["evrede_sn"] <= 3.0]
n = {a: st.mean(s[a] for s in notr) for a in ("egim", "dz", "donme")}
print(f"notr ({len(notr)} kare): egim {n['egim']:+.1f}  dz {n['dz']:+.3f}  donme {n['donme']:+.3f}")
print(f"notr gurultu (std): egim {st.pstdev(s['egim'] for s in notr):.1f}  dz {st.pstdev(s['dz'] for s in notr)*100:.2f} cm")
print("evre etiket  kare  egim(°)   dz(cm)  dz_std  donme(cm)")
for ev in sorted({s["evre"] for s in satirlar}):
    g = [s for s in satirlar if s["evre"] == ev and s["evrede_sn"] >= 1.0]
    if not g:
        continue
    e = st.mean(s["egim"] for s in g) - n["egim"]
    d = (st.mean(s["dz"] for s in g) - n["dz"]) * 100
    ds = st.pstdev(s["dz"] for s in g) * 100
    r = (st.mean(s["donme"] for s in g) - n["donme"]) * 100
    print(f"{ev:4d} {g[0]['etiket']:6s} {len(g):5d} {e:+8.1f} {d:+8.2f} {ds:7.2f} {r:+9.2f}")
