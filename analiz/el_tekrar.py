"""Kamera paketi olmadan gerçek yayın hesabını CSV noktalarıyla tekrarlar.

Kullanım: python analiz/el_tekrar.py olcumler/el_20261017_204716.csv olcumler/tam_20261017_175142.csv
Tarama: aynı komuta --tara ekleyin (12 zaman çifti ve son sakin an denemeleri).
Nötr: ilk dik evresinin 1–3. saniyesi. Geçişler işlenir; yalnız tabloda
her evrenin ilk 1,5 saniyesi dışlanır. Evre sınırında filtre/tutma sıfırlanmaz.
Kare sayısı bu kesmeden sonraki tüm karelerdir; ortalamalar null değerleri
dışlar. Evre_ort, koordinatör gibi tekrar evrelerine eşit ağırlık verir.
Önceki dik referansı serbest ortalamadır; bırakma gecikmesi hedefi değiştirmez.
Öne+el kontrolü kesilen geçişler dahil tüm tutulan kareleri kapsar.
"""

import argparse
import csv
from functools import lru_cache
import math
from pathlib import Path
import statistics
import sys
from types import SimpleNamespace

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from hareket_yayini import (  # noqa: E402
    BIRAK_GECIKME_SN, GERI_BAK_SN, EL_TUT_BASLA, EL_TUT_BIRAK,
    HareketIsleyici, HizTutucu, Kalibrasyon,
    el_olcum, govde_egimi, ham_olcum,
)


def noktalar(satir):
    n = [SimpleNamespace(x=float(satir[f"g{i}_x"]), y=float(satir[f"g{i}_y"]),
                         z=float(satir[f"g{i}_z"]), visibility=float(satir[f"g{i}_v"]))
         for i in range(33)]
    w = [SimpleNamespace(**{eksen: float(satir[f"w{i}_{eksen}"]) for eksen in "xyz"})
         for i in range(33)]
    return SimpleNamespace(pose_landmarks=[n], pose_world_landmarks=[w])


def donus(egim):
    """Motor hesabı: %80 hassasiyet, 3° ölü bölge, 2,5 katsayı, 60°/sn tavan."""
    if egim is None:
        return None
    etkin = max(abs(egim) - 3.0, 0.0)
    return math.copysign(min(etkin * 0.8 * 2.5, 60.0), egim)


def ort(satirlar, alan):
    degerler = [s[alan] for s in satirlar if s[alan] is not None]
    return statistics.mean(degerler) if degerler else None


def say(deger):
    return "null" if deger is None else f"{deger:+.2f}"


def saf_kontroller():
    """Kayıtların kapsamadığı görünürlük, histerezis ve kopma sınırları."""
    n = [SimpleNamespace(x=0.5, y=0.2, visibility=1.0) for _ in range(33)]
    n[23].y = n[24].y = 0.8
    assert govde_egimi(n, 720, 1280) == 0
    n[23].visibility = 0.49
    assert govde_egimi(n, 720, 1280) is None
    n[23].visibility = 1.0
    n[11].visibility = 0.49
    assert govde_egimi(n, 720, 1280) is None
    h = HizTutucu(geri_bak_sn=0.5, birak_gecikme_sn=0.3)
    assert h.besle(0, 0.01, EL_TUT_BASLA, -1.5) == 0.01 and not h.tutuluyor
    assert h.besle(0.1, 0.012, -0.99, -1.5) == 0.01 and h.tutuluyor  # en eski
    assert h.besle(0.2, -0.06, -1.1, -1.5) == 0.01
    assert h.besle(0.3, -0.06, EL_TUT_BIRAK, -1.5) == 0.01
    assert h.besle(0.4, 0.02, -1.21, -1.5) == 0.01 and h.tutuluyor
    # Görünen el eşik altındaysa eksik el bırakmayı engellemez; gecikme sürer.
    assert h.besle(0.6, 0.02, None, -1.5) == 0.01
    assert h.besle(0.7, 0.02, -1.21, -1.5) == 0.01
    assert h.besle(0.99, 0.02, -1.21, -1.5) == 0.02 and not h.tutuluyor
    # Tek el sürekli görünmezken görünen elin inişi tutmayı bırakabilmeli.
    h.sifirla()
    h.besle(0, 0.01, None, -1.5)
    assert h.besle(0.1, 0.01, None, -0.9) == 0.01 and h.tutuluyor
    assert h.besle(0.2, 0.02, None, -1.21) == 0.01 and h.tutuluyor
    assert h.besle(0.5, 0.02, None, -1.21) == 0.02 and not h.tutuluyor
    # Beş saniye kalkık kalan elde 4 sn'de bırak; inişe dek kilitli kal.
    h = HizTutucu(geri_bak_sn=0.5, birak_gecikme_sn=0)
    h.besle(0, 0.01, 0, -1.5)
    assert h.besle(0.1, 0.02, 0, -0.9) == 0.01 and h.tutuluyor
    for i in range(1, 50):
        t = 0.1 + i * 0.1
        v = h.besle(t, 0.02, 0, -0.9)
    assert not h.tutuluyor and v == 0.02
    h.besle(5.1, 0.03, 0, -1.3)
    h.besle(5.2, 0.04, 0, -0.9)
    h.sifirla()
    h.besle(1, 0.01, -1.5, 0)
    assert h.besle(2, -0.05, None, None) == 0.01
    assert h.besle(2.499, -0.05, None, None) == 0.01
    assert h.besle(2.5, -0.05, None, None) == -0.05 and not h.tutuluyor
    h.sifirla()
    h.besle(3, 0.01, 0, -0.9)
    # Yeni kural: görünen tek el eşik altındaysa eksik el bırakmayı engellemez (gecikme 0).
    assert h.besle(3.01, -0.05, None, -1.5) == -0.05 and not h.tutuluyor
    assert h.besle(3.1, None, 0, -1.5) is None and not h.tutuluyor
    h.besle(4, 0.01, 0, -1.5)
    h.sifirla()
    assert not h.tutuluyor and h.deger is None
    h = HizTutucu(geri_bak_sn=0.5)
    for t, o in ((0, 0.01), (1.0, 0.02), (1.4, 0.03), (1.8, 0.04)):
        h.besle(t, o, -1.5, -1.5)
    assert h.besle(2, -0.06, 0, -1.5) == 0.03  # hedefin solundaki son örnek
    assert all(t >= 0.5 for t, _ in h.gecmis)
    assert h.besle(2.1, None, 0, -1.5) is None and not h.gecmis
    h = HizTutucu(sakin_esik=-1.4)
    h.besle(0, 0.01, -1.5, -1.5)
    h.besle(0.1, -0.04, -1.3, -1.5)
    assert h.besle(0.2, -0.06, 0, -1.5) == 0.01
    kal = Kalibrasyon()
    kal.notr_al([(0.0, 0.3, None)])
    isleyici = HareketIsleyici()
    assert isleyici.besle(0, kal, 0, 0.3, 0, -1.5)["tut"]
    assert not isleyici.besle(1, kal, None, 0.3, 0, -1.5)["tut"]
    isleyici.besle(2, kal, 0, 0.3, 0, -1.5)
    kal.sifirla()
    assert not isleyici.besle(3, kal, 0, 0.3, 0, -1.5)["tut"]


@lru_cache(maxsize=None)
def kayit_oku(yol):
    with open(yol, newline="", encoding="utf-8-sig") as dosya:
        ham = list(csv.DictReader(dosya))
    dik_evre = next(s["evre"] for s in ham if s["etiket"] == "dik")
    olcumler = []
    for s in ham:
        sonuc = noktalar(s)
        g, y = int(s["gen"]), int(s["yuk"])
        egim, omuz, dz = ham_olcum(sonuc, g, y)
        assert egim == govde_egimi(sonuc.pose_landmarks[0], g, y)
        sol, sag, _, _ = el_olcum(sonuc, g, y, egim, omuz)
        olcumler.append(dict(s, aci=egim, omuz_orani=omuz, ham_dz=dz, sol_el=sol, sag_el=sag))
    notrler = [s for s in olcumler if s["evre"] == dik_evre and 1 <= float(s["evrede_sn"]) <= 3
               and s["aci"] is not None]
    if not notrler:
        raise ValueError(f"{yol}: ilk dik evresinde geçerli nötr örneği yok")
    kal = Kalibrasyon()
    kal.notr_al([(s["aci"], s["omuz_orani"], s["ham_dz"]) for s in notrler])
    notr_omuz_egim = statistics.mean(float(s["egim"]) for s in notrler)
    # Nötr örnekleme bitmeden kontrol üretilmez. Geçiş kareleri dahil sonraki
    # tüm kareler işlenir; raporun 1,5 sn kesmesi filtrenin belleğini etkilemez.
    kal_bitis = next(float(s["t"]) for s in olcumler
                     if s["evre"] == dik_evre and float(s["evrede_sn"]) >= 3)
    return olcumler, kal, notr_omuz_egim, kal_bitis


def tekrar(yol, **ayar):
    olcumler, kal, notr_omuz_egim, kal_bitis = kayit_oku(yol)
    isleyici = HareketIsleyici(**ayar)
    evreler = {}
    ilk_tut = None
    sira = {}
    for s in olcumler:
        simdi = float(s["t"])
        onceki_one = isleyici.duz_one
        onceki_tut = isleyici.tutucu.tutuluyor
        if simdi >= kal_bitis:
            mesaj = isleyici.besle(simdi, kal, s["aci"], s["omuz_orani"], s["sol_el"], s["sag_el"])
        else:
            mesaj = {"egim": None, "one": None, "tut": False}
        olay = sira.setdefault((s["evre"], s["etiket"]), {})
        if isleyici.duz_one is not None and isleyici.duz_one * 100 >= 1.5:
            olay.setdefault("one_sn", float(s["evrede_sn"]))
        if any(el is not None and el > EL_TUT_BASLA for el in (s["sol_el"], s["sag_el"])):
            olay.setdefault("el_sn", float(s["evrede_sn"]))
        if mesaj["tut"] and not onceki_tut:
            olay.setdefault("tut_cm", mesaj["one"] * 100)
            olay.setdefault("kaynak_sn", float(s["evrede_sn"]) - (simdi - isleyici.tutucu.kaynak_zamani))
        if mesaj["tut"]:
            olay["sabit_min"] = min(olay.get("sabit_min", math.inf), mesaj["one"] * 100)
        if mesaj["tut"] and not onceki_tut and ilk_tut is None:
            onceki_cm = onceki_one * 100 if onceki_one is not None else None
            ilk_tut = (f"{s['evre']}/{s['evrede_sn']};onceki_cm={say(onceki_cm)};"
                       f"simdi_cm={say(isleyici.duz_one*100)};sabit_cm={say(mesaj['one']*100)};"
                       f"kaynak_sn={say(olay['kaynak_sn'])}")
        if float(s["evrede_sn"]) < 1.5:
            continue
        evreler.setdefault((s["evre"], s["etiket"]), []).append({
            "egim": mesaj["egim"], "donus": donus(mesaj["egim"]),
            "tut_cm": mesaj["one"] * 100 if mesaj["one"] is not None else None,
            "serbest_cm": round(isleyici.duz_one, 4) * 100 if isleyici.duz_one is not None else None,
            "sol_el": s["sol_el"], "sag_el": s["sag_el"], "tut": mesaj["tut"],
            "evrede_sn": s["evrede_sn"],
            "govde": s["aci"] - kal.notr_egim if s["aci"] is not None else None,
            "omuz": float(s["egim"]) - notr_omuz_egim,
        })
    return evreler, ilk_tut, sira


def ozetler(evreler, sira=None):
    onceki_dik = None
    sonuc = {}
    for anahtar, ss in evreler.items():
        sonuc[anahtar] = {alan: ort(ss, alan) for alan in
                         ("egim", "donus", "tut_cm", "serbest_cm", "sol_el", "sag_el", "tut")}
        o = sonuc[anahtar]
        # Referans serbest dik ortalaması: bırakma gecikmesi hedefi kaydırmasın.
        o["onceki_dik"] = onceki_dik
        o["fark"] = o["tut_cm"] - onceki_dik if o["tut_cm"] is not None and onceki_dik is not None else None
        o["sabit_min"] = min((s["tut_cm"] for s in ss if s["tut"]), default=None)
        if sira is not None:
            o["sabit_min"] = sira[anahtar].get("sabit_min")
        if anahtar[1] == "dik":
            onceki_dik = o["serbest_cm"]
    return sonuc


def hedef(ozet):
    el = [o["fark"] for (_, etiket), o in ozet.items() if etiket in ("sol_el", "sag_el")]
    one = [o["sabit_min"] for (_, etiket), o in ozet.items() if etiket.startswith("one_") and "el" in etiket]
    return el, one, (len(el) == len(one) == 4 and all(abs(d) <= 1.5 for d in el)
                     and all(d is not None and d >= 1.5 for d in one))


def tablo(yol, sonuc):
    evreler, ilk_tut, sira = sonuc
    ozet = ozetler(evreler, sira)
    print(Path(yol).name)
    print("evre/etiket|kare|egim_deg|donus_deg/sn|tut_cm|serbest_cm|sol_el|sag_el|tut_%|onceki_dik_cm|fark_cm")
    for (evre, etiket), ss in evreler.items():
        o = ozet[(evre, etiket)]
        print(f"{evre}/{etiket}|{len(ss)}|" + "|".join(say(ort(ss, alan)) for alan in
              ("egim", "donus", "tut_cm", "serbest_cm", "sol_el", "sag_el"))
              + f"|{ort(ss, 'tut') * 100:.1f}|{say(o['onceki_dik'])}|{say(o['fark'])}")
    sarkik = [(el, evre, s["evrede_sn"]) for (evre, etiket), ss in evreler.items() if "el" not in etiket
              for s in ss for el in (s["sol_el"], s["sag_el"]) if el is not None]
    if sarkik:
        tepe, evre, sn = max(sarkik, key=lambda s: s[0])
        print(f"Sarkik_max={tepe:.2f}@{evre}/{sn};baslama_payi={EL_TUT_BASLA-tepe:.2f};birakma_payi={EL_TUT_BIRAK-tepe:.2f}")
    else:
        print("Sarkik_max=null")
    if ilk_tut is not None:
        print("Ilk_tut=" + ilk_tut)
    for etiket in ("sag", "sol", "sol_el", "sag_el"):
        gruplar = [ss for (_, ad), ss in evreler.items() if ad == etiket]
        if gruplar:
            govdeler = [ort(ss, "govde") for ss in gruplar]
            omuzlar = [ort(ss, "omuz") for ss in gruplar]
            govde = statistics.mean(v for v in govdeler if v is not None) if any(v is not None for v in govdeler) else None
            omuz = statistics.mean(v for v in omuzlar if v is not None) if any(v is not None for v in omuzlar) else None
            print(f"Evre_ort/{etiket}:govde={say(govde)};omuz={say(omuz)}")
    eller = [s for (_, etiket), ss in evreler.items() if "el" in etiket for s in ss]
    sol_evreleri = [ss for (_, etiket), ss in evreler.items() if etiket == "sol_el"]
    if sol_evreleri:
        farklar, one, hiz_ok = hedef(ozet)
        donusler = [abs(s["donus"]) for s in eller if s["donus"] is not None]
        donus_tepe = max(donusler) if donusler else None
        print(f"Hiz_hedefi={'GECTI' if hiz_ok else 'KALDI'};el_donus_tepe={say(donus_tepe)}")
        print("El_farklari=" + "/".join(say(d) for d in farklar)
              + ";one_sabit_min=" + "/".join(say(d) for d in one))
    for (evre, etiket), olay in sira.items():
        if etiket.startswith("one_") and "el" in etiket:
            print(f"Sira/{evre}:one_1.5cm_sn={say(olay.get('one_sn'))};"
                  f"el_-1_sn={say(olay.get('el_sn'))};sabit_cm={say(olay.get('tut_cm'))};"
                  f"kaynak_sn={say(olay.get('kaynak_sn'))}")


def tara(yollar):
    sonuclar = []
    # Sakin seçenek zaman eşiği kullanmaz; -1,3/-1,4/-1,5 seviyelerini de karşılaştır.
    ayarlar = [dict(geri_bak_sn=g, birak_gecikme_sn=b) for g in (0.3, 0.5, 0.8, 1.0)
               for b in (0, 0.3, 0.5)]
    ayarlar += [dict(sakin_esik=s, birak_gecikme_sn=b) for s in (-1.3, -1.4, -1.5)
                for b in (0, 0.3, 0.5)]
    eski = {yol: ozetler(tekrar(yol, geri_bak_sn=0, birak_gecikme_sn=0)[0]) for yol in yollar}
    print("mod|geri_sn/sakin_esik|birak_sn|fark_2/4/10/12_cm|one_sabit_min_6/8/14/16_cm|hedef")
    for ayar in ayarlar:
        hedefler = []
        for yol in yollar:
            evreler, _, sira = tekrar(yol, **ayar)
            yeni = ozetler(evreler, sira)
            for k, o in yeni.items():
                assert all(o[a] == eski[yol][k][a] for a in ("egim", "donus", "serbest_cm"))
            if any(k[1] in ("sol_el", "sag_el") for k in yeni):
                hedefler.append(hedef(yeni))
            else:
                assert all(o["tut"] == 0 and o["tut_cm"] == eski[yol][k]["tut_cm"]
                           for k, o in yeni.items())
        if not hedefler:
            raise ValueError("Tarama için el kaydı gerekli")
        farklar = [d for f, _, _ in hedefler for d in f]
        one = [d for _, o, _ in hedefler for d in o]
        gecti = all(g for _, _, g in hedefler)
        ihlal = [max(abs(d) - 1.5, 0) for d in farklar] + [max(1.5 - d, 0) if d is not None else 100 for d in one]
        mod = "sakin" if "sakin_esik" in ayar else "zaman"
        satir = (f"{mod}|{ayar.get('sakin_esik', ayar.get('geri_bak_sn'))}|{ayar['birak_gecikme_sn']}|"
                 + "/".join(say(d) for d in farklar) + "|" + "/".join(say(d) for d in one)
                 + f"|{'GECTI' if gecti else 'KALDI'}")
        print(satir)
        # Geçenlerde en kısa geriye bakma ve bırakma; kalanda en küçük en kötü ihlal.
        sira = ((0, ayar.get("geri_bak_sn", 0), ayar["birak_gecikme_sn"], max(map(abs, farklar))) if gecti
                else (1, max(ihlal), sum(ihlal), ayar["birak_gecikme_sn"]))
        sonuclar.append((sira, satir))
    for i, (_, satir) in enumerate(sorted(sonuclar)[:3], 1):
        print(f"En_iyi_{i}=" + satir)
    print("Koruma=GECTI;egim/donus/serbest_cm_ayni;elsiz_kayit_tut_%=0")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--tara", action="store_true")
    parser.add_argument("kayitlar", nargs="+")
    args = parser.parse_args()
    saf_kontroller()
    print("Saf_kontroller=GECTI")
    print(f"Ayar:geri_bak_sn={GERI_BAK_SN};birak_gecikme_sn={BIRAK_GECIKME_SN}")
    if args.tara:
        tara(args.kayitlar)
    else:
        for yol in args.kayitlar:
            tablo(yol, tekrar(yol))


if __name__ == "__main__":
    main()
