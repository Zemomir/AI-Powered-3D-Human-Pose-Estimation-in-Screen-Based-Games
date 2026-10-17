"""Gerçek kamera açmadan nötr ölçümünün pozisyon, hareket ve süre kontrolleri."""
import csv
import io
import json
from pathlib import Path
import statistics
import sys
from types import SimpleNamespace

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import hareket_yayini as h


def sabit(kal, bas, son, egim=2.0, omuz=.3):
    for i in range(round((son - bas) * 100) + 1):
        kal.besle(round(bas + i / 100, 6), egim, omuz, -.1)


def kontroller():
    kal = h.Kalibrasyon()
    kal.besle(0, None, None, None)
    kal.besle(50, None, .3, None)  # Omuz var, kalça yok: sayaç başlamaz.
    assert kal.basla is None and kal.kalan(50) == 3.5
    sabit(kal, 51, 54.49)
    assert not kal.tamam and kal.deneme == 1
    kal.besle(54.5, 2, .3, -.1)
    assert kal.tamam and kal.notr_egim == 2 and kal.notr_omuz == .3
    assert kal.kalan(54.5) == 0
    print("Pozisyon_sabit=GECTI (tek deneme; pozisyondan sonra 3.50 sn)")

    for alan in ("egim", "omuz"):
        kal = h.Kalibrasyon()
        sabit(kal, 0, 1.5, egim=0)
        kal.besle(1.6, 10 if alan == "egim" else 0, .36 if alan == "omuz" else .3, -.1)
        assert kal.deneme == 2 and len(kal.ornekler) == 1
        assert abs(kal.kalan(1.6) - 2.5) < 1e-9
        # Yeni duruşa yerleşmişse ikinci pencerede tekrar 1 sn beklemez.
        sabit(kal, 1.7, 4.09, egim=10 if alan == "egim" else 0,
              omuz=.36 if alan == "omuz" else .3)
        assert not kal.tamam
        kal.besle(4.1, 10 if alan == "egim" else 0, .36 if alan == "omuz" else .3, -.1)
        assert kal.tamam and kal.deneme == 2
    print("Kipirdama=GECTI (govde 4 derece / omuz %8; tekrar 2.50 sn)")

    # İlk 3 başarısızlık sonrası 4. denemenin 8 derece / %16 payı.
    for alan in ("egim", "omuz"):
        kal = h.Kalibrasyon()
        sabit(kal, 0, 1.2, egim=0)
        for i in range(3):
            for j in range(10):
                v = 40 if i % 2 == 0 else 0
                kal.besle(1.3 + i * .2 + j * .01, v, .3, None)
        assert kal.deneme == 4 and not kal.tamam
        t = 2.0
        # Mevcut pencereyi kendi merkezi çevresinde hafifçe oynat.
        for i in range(260):
            kal.besle(t + i / 100, 40 + (6 if i % 2 else 0) if alan == "egim" else 40,
                      .3 + (.04 if i % 2 else 0) if alan == "omuz" else .3, None)
        assert kal.tamam and kal.deneme == 4
    print("Esnetme=GECTI (3 basarisizliktan sonra tolerans iki kat)")

    kal = h.Kalibrasyon()
    sabit(kal, 0, 1.2, egim=0)
    for i in range(100):
        t = round(1.3 + i / 10, 6)
        kal.besle(t, 40 if i % 2 else 0, .6 if i % 2 else .3, -.1)
        if kal.deneme == 6:
            son_bas = t
            break
    assert kal.deneme == 6 and not kal.tamam
    for i in range(1, 25):
        kal.besle(round(son_bas + i / 10, 6), 40 if i % 2 else 0, .6 if i % 2 else .3, -.1)
        assert not kal.tamam  # Zorla kabul de tam 2,5 sn pencere toplar.
    kal.besle(round(son_bas + 2.5, 6), 40, .6, -.1)
    assert kal.tamam and kal.deneme == 6
    assert kal.notr_egim == statistics.mean(e for e, _, _ in kal.ornekler)
    assert kal.notr_omuz == statistics.median(o for _, o, _ in kal.ornekler)
    print("Zorla_kabul=GECTI (6. deneme; son tam pencerenin ortalama/medyani)")

    kal.sifirla()
    sabit(kal, 0, 2)
    kal.besle(2.1, None, .3, None)
    assert kal.basla is None and not kal.ornekler and kal.kalan(2.1) == 3.5
    sabit(kal, 10, 13.5)
    assert kal.tamam and kal.deneme == 1
    print("Kayip_sifirlama=GECTI (gorunurluk kaybi ve yeni kalibrasyonda deneme sifirlanir)")

    mesajlar, tampon = [], io.StringIO()
    mesaj = h.sinyal_yok(1)
    mesaj.update(durum="kalibrasyon", kalibrasyon_kalan=2.5, kalibrasyon_deneme=2)
    alanlar = list(h.sinyal_yok(0)) + ["kalibrasyon_kalan"]
    yazici = csv.DictWriter(tampon, fieldnames=alanlar)
    yazici.writeheader()
    durum = {"yazici": yazici, "kayit": tampon, "son_flush": h.time.monotonic(), "uyari": False}
    h.gonder(SimpleNamespace(sendto=lambda veri, _: mesajlar.append(json.loads(veri))), mesaj, durum)
    assert not durum["uyari"] and mesajlar[0]["kalibrasyon_deneme"] == 2
    assert list(csv.DictReader(io.StringIO(tampon.getvalue())))[0]["kalibrasyon_kalan"] == "2.5"
    assert tampon.getvalue().splitlines()[0].endswith("kalibrasyon_kalan")
    print("Mesaj_CSV=GECTI (UDP kalan/deneme; CSV son sutun kalan)")


if __name__ == "__main__":
    kontroller()
