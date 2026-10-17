"""Gerçek kamera/paket yüklemeden otomatik aramanın saf ve sahte kaynak kontrolleri."""
import contextlib
import csv
import io
import json
from pathlib import Path
import queue
import sys
import threading
from types import SimpleNamespace as N
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import hareket_yayini as h


def noktalar():
    n = [N(x=.5, y=.2, visibility=1) for _ in range(33)]
    n[11], n[12] = N(x=.4, y=.4, visibility=1), N(x=.6, y=.4, visibility=1)
    n[23], n[24] = N(x=.4, y=.7, visibility=1), N(x=.6, y=.7, visibility=1)
    return n


def saf_kontroller():
    assert not h.kareler_gecerli([])
    assert not h.kareler_gecerli([[50], [51]])
    assert not h.kareler_gecerli([[50], [51, 52], [53]])
    assert not h.kareler_gecerli([[0, 1], [3, 4], [1, 2]])
    assert not h.kareler_gecerli([[100, 140]] * 12)
    assert not h.kareler_gecerli([[100], [100.2], [100.3]])
    assert h.kareler_gecerli([[100, 140], [103, 141], [101, 144]])
    n = noktalar()
    assert h.kisi_dik(n)
    for p in n:
        p.y = 1 - p.y
    assert not h.kisi_dik(n)
    n = noktalar()
    n[23].visibility = n[24].visibility = 0
    assert h.kisi_dik(n)
    n[0].y = .8
    assert not h.kisi_dik(n)
    n = noktalar()
    n[23].y = n[24].y = 1.4  # kalça kadraj dışı: burun kuralı
    assert h.kisi_dik(n)
    n[11].visibility = .1
    assert not h.kisi_dik(n)
    assert not h.kisi_dik([])
    n = noktalar()
    n[23].x = n[24].x = .9
    n[23].y = n[24].y = .41
    assert not h.kisi_dik(n)
    print("Saf_kamera_kontrolleri=GECTI (15)")


class Kare:
    def __init__(self, kaynak, deger, aci=0):
        self.kaynak, self.deger, self.aci = kaynak, deger, aci

    def reshape(self, _):
        return self

    def tolist(self):
        return [self.deger] * 16


class SahteDur:
    def __init__(self, iptal=None):
        self.zaman, self.iptal = 0, iptal

    def is_set(self):
        return self.iptal is not None and self.zaman >= self.iptal

    def wait(self, sn):
        self.zaman += sn
        return self.is_set()


def arama_kontrolu(ayarlar, hedef=None, iptal=None, bekle=0, hata=False):
    dur, acilan, denenen = SahteDur(iptal), [], []

    class Kamera:
        def __init__(self, kaynak):
            denenen.append((kaynak, dur.zaman))
            if kaynak not in ayarlar:
                raise SystemExit(2)
            self.kaynak, self.tur = kaynak, ayarlar[kaynak]
            self.no, self.kapanis, self.calisiyor = 0, 0, True
            acilan.append(self)

        def al(self):
            self.no += 1
            if self.tur == "bos":
                return self.no, None, dur.zaman
            deger = 2 + self.no % 2 if self.tur == "karanlik" else 100
            if self.tur == "canli":
                deger += 3 * (self.no % 2)
            return self.no, Kare(self.kaynak, deger), dur.zaman

        def kapat(self):
            self.kapanis += 1
            self.calisiyor = False

    class Model:
        def __enter__(self):
            return self

        def __exit__(self, *_):
            pass

        def detect(self, kare):
            if hata:
                raise RuntimeError("sahte model hatasi")
            return N(pose_landmarks=[noktalar()] if hedef == (kare.kaynak, kare.aci) else [])

    cv = N(error=RuntimeError, COLOR_BGR2GRAY=0, COLOR_BGR2RGB=1,
           resize=lambda k, _: k, cvtColor=lambda k, _: k,
           rotate=lambda k, a: Kare(k.kaynak, k.deger, a))
    ek = dict(cv2=cv, mp=N(Image=lambda **kw: kw["data"], ImageFormat=N(SRGB=0)),
              vision=N(PoseLandmarker=N(create_from_options=lambda _: Model())),
              DONDURME={0: None, 90: 90, 180: 180, 270: 270},
              time=N(monotonic=lambda: dur.zaman), kamera_ac=lambda k, d, s: s(k))
    sonuc, kod, stderr = None, None, io.StringIO()
    telefon_durumu = {"yeniden_ac": bekle}
    with patch.multiple(h, create=True, **ek), contextlib.redirect_stderr(stderr):
        try:
            sonuc = h.kamera_bul("http://telefon/video", dur, Kamera, None, telefon_durumu)
        except SystemExit as e:
            kod = e.code
        except RuntimeError:
            assert hata
    for k in acilan:
        assert k.kapanis == (0 if sonuc and k is sonuc[0] else 1)
    assert len({k for k, _ in denenen}) == len(denenen)  # her kaynak en çok bir kez açılır
    for k in acilan:
        if k.kaynak == "http://telefon/video" and k.kapanis:
            assert telefon_durumu["yeniden_ac"] >= dur.zaman + 15
    return sonuc, kod, stderr.getvalue(), denenen


def sahte_kontroller():
    s, _, _, ac = arama_kontrolu({0: "canli"}, (0, 0))
    assert s[1:] == ("laptop:0", 0) and len(ac) == 1
    s, _, _, _ = arama_kontrolu({0: "canli", 1: "canli", "http://telefon/video": "canli"}, ("http://telefon/video", 270))
    assert s[1:] == ("telefon", 270)
    s, _, _, _ = arama_kontrolu({0: "canli", 1: "canli", "http://telefon/video": "canli"})
    assert s[1:] == ("laptop:0", 0)  # kişi yok: ilk geçerli kaynak aynı nesneyle kalır
    s, _, _, ac = arama_kontrolu({0: "karanlik", 1: "logo", 2: "bos", "http://telefon/video": "canli"}, bekle=15)
    assert s[1:] == ("telefon", 90) and ac[-1][1] >= 15
    s, kod, sebep, _ = arama_kontrolu({0: "karanlik", 1: "logo", 2: "bos"})
    assert s is None and kod == 2 and "Kamera bulunamadı" in sebep and "telefon http://telefon/video" in sebep
    s, _, _, _ = arama_kontrolu({0: "canli"}, iptal=.3)
    assert s is None
    s, _, _, _ = arama_kontrolu({0: "canli"}, hata=True)
    assert s is None
    print("Sahte_arama=GECTI (7 senaryo; tek acilis, secilmeyenler kapali, Busy beklemesi, kod 2)")


def mesaj_komut_kontrolu():
    kal, isleyici = h.Kalibrasyon(), h.HareketIsleyici()
    kal.notr_egim, isleyici.duz_one = 1, .1
    q = queue.Queue()
    q.put({"komut": "yeniden_ara"})
    assert h.komutlari_oku(q, kal, isleyici, threading.Event(), 90, False) == (90, False, True)
    assert not kal.tamam and isleyici.duz_one is None
    tampon, mesajlar = io.StringIO(), []
    yazici = csv.DictWriter(tampon, fieldnames=list(h.sinyal_yok(0)) + ["aci", "kaynak"])
    durum = {"kamera_bilgisi": {"kaynak": "telefon", "aci": 90}, "yazici": yazici,
             "kayit": tampon, "son_flush": h.time.monotonic(), "uyari": False}
    h.gonder(N(sendto=lambda veri, _: mesajlar.append(json.loads(veri))), h.sinyal_yok(1), durum)
    assert mesajlar[0]["kaynak"] == "telefon" and mesajlar[0]["aci"] == 90
    assert tampon.getvalue().rstrip().endswith(",90,telefon")
    print("Mesaj_CSV_yeniden_ara=GECTI")


def arama_bildirim_kontrolu():
    mesajlar, uc_mesaj = [], threading.Event()

    def gonder(veri, _):
        mesajlar.append(json.loads(veri))
        if len(mesajlar) >= 3:
            uc_mesaj.set()

    def bekleyen_arama(*_):
        assert uc_mesaj.wait(1.0)
        return "sahte sonuc"

    kayit = {"yazici": None, "kamera_bilgisi": {"kaynak": "telefon", "aci": 90}}
    with patch.object(h, "kamera_bul", bekleyen_arama):
        sonuc = h.arayarak_ac(None, threading.Event(), None, None, N(sendto=gonder), kayit, {"yeniden_ac": 0})
    assert sonuc == "sahte sonuc" and len(mesajlar) >= 3
    assert all(m["durum"] == "araniyor" and not m["kalibre"] and
               "kaynak" not in m and "aci" not in m for m in mesajlar)
    print("Arama_bildirimi=GECTI (ardisik 3 araniyor mesaji)")


if __name__ == "__main__":
    saf_kontroller()
    sahte_kontroller()
    mesaj_komut_kontrolu()
    arama_bildirim_kontrolu()
