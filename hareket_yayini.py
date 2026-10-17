"""Kameradan gövde hareketini okuyup oyuna (Unity) UDP ile gönderir.
Oyun içi hissi ölçüm verisiyle karşılaştırmak için gönderilen mesajları CSV'ye kaydeder.

Her karede 127.0.0.1:5065'e tek satırlık JSON gider; CSV kaydı saat,t,kalibre,egim,one,donuk,ham_egim,ham_dz,notr_egim,notr_dz,ham_omuz,notr_omuz,sol_el,sag_el,sol_bilek_gor,sag_bilek_gor,tut,ham_omuz_egim sütunlarını içerir:
    {"t": 12.345, "kalibre": true, "egim": 8.2, "one": 0.031, "sol_el": -1.5, "sag_el": 0.6, "tut": true}
  egim : kalça ortası -> omuz ortası açısının nötre göre farkı * GOVDE_OLCEK; + = kişi sağına eğik.
  ham_egim/notr_egim: ölçeklenmemiş gövde açısı/nötrü; ham_omuz_egim: karşılaştırma için omuz çizgisi.
  one  : öne/geriye eğilme, omuz genişliği oranından metre eşdeğeri; + = öne, nötre göre.
  donuk: true ise telefondan kare gelmiyor (Python çalışıyor ama akış dondu).
  sol_el/sag_el: (omuz_y - bilek_y) / omuz mesafesi, piksel cinsinden; + = bilek omuzdan yukarıda.
  Taraflar eğimle aynı ekran x kuralını izler: x'i büyük omuz sağdır; kalibrasyon ve yumuşatma uygulanmaz.
  Görünmeyen bilek null, karenin üstüne çıkan bilek düşük görünürlükte de ölçülür; donuk karede iki el null.
  tut: el kalkarken geçmişteki yumuşatılmış one sabitlenir; iniş beklemesinden sonra serbest kalır.
  Kişi, omuzlar ya da kalçalar görünmüyorsa egim ve one null, tut false olur ("sinyal yok").
  Kamera donarsa her SESSIZ_ARALIK_SN'de null mesaj, çıkışta da son bir null mesaj gider. Yine de
  oyun ~0,5 sn mesaj almazsa kendisi "sinyal yok" saymalı (betik çökerse null gönderemez).

Kalibrasyon: kişi göründükten sonra YERLESME_SN atlanır (yerleşme, c'ye basan elin dönüşü),
sonraki KALIBRASYON_SN'de nötr alınır: gövde açısı ortalamayla (çocuğun dinlenme duruşu asimetrik olabilir),
omuz genişliği medyanla (tek tük kötü kareye dayanıklı). c tuşu yeniden kalibre eder.

Kullanım:
    .venv\\Scripts\\python.exe hareket_yayini.py http://192.168.1.144:4747/video
Tuşlar: c yeniden kalibre, q / Esc çıkış.
"""

import json
import csv
import math
import os
import socket
import sys
import time
import statistics
from collections import deque

# Saf ölçüm ve tekrar analizi kamera paketleri olmadan import edilebilir.
OMUZLAR, KALCALAR = (11, 12), (23, 24)
MIN_GORUNURLUK = 0.5  # kamera_test ile aynı omuz görünürlüğü koşulu.

HEDEF = ("127.0.0.1", 5065)
KALIBRASYON_SN = 3.0
YERLESME_SN = 1.0  # 2026-10-17 deneme 1: yerleşme payı olmayan kalibrasyon kirlendi
SESSIZ_ARALIK_SN = 0.1
# Oyun 0,5 sn mesaj almazsa "Python çalışmıyor" der; donma bundan önce bildirilsin ki
# ekranda yanlış teşhis ("kamera bağlı değil") yanıp sönmesin.
DONUK_ESIK_SN = 0.3
YUMUSATMA = 0.5  # üstel ortalama: 1 = ham, küçüldükçe daha yumuşak ama daha gecikmeli
ONE_OLCEK = 0.42  # tam_20261017_175142.csv: +%10,6 yaklaşık 4,5 cm; tek kişiden, ileride kişiye göre kalibre edilebilir.
GERI_OLCEK = 1.0  # geri -%4,3: küçük geriye eğilmede yavaşlama da hızlanma kadar kolay olsun.
GOVDE_OLCEK = 1.6  # tam_20261017_175142.csv: omuz/gövde oranı 1,35–1,89, ortalama ~1,6.
EL_TUT_BASLA = -1.0
EL_TUT_BIRAK = -1.2
EL_TUT_NULL_SN = 0.5
AZAMI_TUT_SN = 4.0
EL_GECMIS_SN = 1.5
# 2026-10-17, analiz/el_tekrar.py --tara, el_20261017_204716 + tam_20261017_175142:
# 4 el farkı <=1,5 cm, öne+el sabitleri >=1,5 cm sağlayan en küçük zaman çifti.
# Son sakin el seviyesi (-1,3/-1,4/-1,5) sağ el bozulmasını gideremedi.
GERI_BAK_SN = 0.3
BIRAK_GECIKME_SN = 0.0
PENCERE = "Fizyo-Oyun hareket yayini"


def govde_egimi(n, g, y):
    """Kalçadan omuza piksel açısı (derece); + = kişi sağına eğik, görünmüyorsa None."""
    if min(n[i].visibility or 0 for i in OMUZLAR + KALCALAR) < MIN_GORUNURLUK:
        return None
    ox = (n[11].x + n[12].x) / 2 * g
    oy = (n[11].y + n[12].y) / 2 * y
    kx = (n[23].x + n[24].x) / 2 * g
    ky = (n[23].y + n[24].y) / 2 * y
    aci = math.degrees(math.atan2(ox - kx, ky - oy))
    return aci if math.isfinite(aci) else None


def ham_olcum(sonuc, g, y):
    """(gövde açısı, omuz oranı, dz); omuzlar yoksa hepsi, kalça yoksa açı/dz None."""
    if not sonuc.pose_landmarks:
        return None, None, None
    n = sonuc.pose_landmarks[0]
    if min(n[i].visibility or 0 for i in OMUZLAR) < MIN_GORUNURLUK:
        return None, None, None
    egim = govde_egimi(n, g, y)
    # Kare genişliğine oran: akışın çözünürlüğü değişirse (yeniden bağlanma) nötrle oran bozulmasın.
    omuz_px = math.hypot((n[11].x - n[12].x) * g, (n[11].y - n[12].y) * y) / g
    dz = None
    if sonuc.pose_world_landmarks and min(n[i].visibility or 0 for i in KALCALAR) >= MIN_GORUNURLUK:
        w = sonuc.pose_world_landmarks[0]
        dz = (w[11].z + w[12].z) / 2 - (w[23].z + w[24].z) / 2
    return egim, omuz_px, dz


def el_olcum(sonuc, g, y, egim, omuz_orani):
    """(sol_el, sag_el, sol_bilek_gor, sag_bilek_gor); omuz_orani ham_olcum ile aynıdır."""
    if egim is None or omuz_orani is None or omuz_orani < 0.01:
        return None, None, None, None
    n = sonuc.pose_landmarks[0]
    # omuz_egimi gibi ekran x sırası: x'i büyük omuz SAĞ; MediaPipe taraf etiketi kullanılmaz.
    esler = sorted(((11, 15), (12, 16)), key=lambda es: n[es[0]].x)
    eller, gorunurlukler = [], []
    for omuz, bilek in esler:
        b = n[bilek]
        gor = b.visibility or 0.0
        gorunurlukler.append(round(gor, 3) if math.isfinite(gor) else None)
        el = (n[omuz].y - b.y) * y / (omuz_orani * g)
        if (0 <= b.y <= 1 and gor < 0.3) or not math.isfinite(el):
            eller.append(None)
        else:
            eller.append(round(el, 2))
    return eller[0], eller[1], gorunurlukler[0], gorunurlukler[1]


class Kalibrasyon:
    def __init__(self):
        self.sifirla()

    def sifirla(self):
        self.ornekler, self.basla, self.notr_egim, self.notr_omuz, self.notr_dz = [], None, None, None, None

    @property
    def tamam(self):
        return self.notr_egim is not None

    def besle(self, simdi, egim, omuz_px, dz):
        """Kalibrasyon sürerken örnek toplar. Kişi kaybolursa sayaç baştan başlar."""
        if self.tamam:
            return
        if egim is None:
            self.ornekler, self.basla = [], None
            return
        if self.basla is None:
            self.basla = simdi
        gecen = simdi - self.basla
        if gecen < YERLESME_SN:
            return
        self.ornekler.append((egim, omuz_px, dz))
        if gecen >= YERLESME_SN + KALIBRASYON_SN:
            self.notr_al(self.ornekler)

    def notr_al(self, ornekler):
        """Geçerli nötr örneklerini özetler; kayıt tekrarı da aynı hesabı kullanır."""
        self.notr_egim = statistics.mean(e for e, _, _ in ornekler)
        omuzlar = [o for _, o, _ in ornekler if o is not None]
        self.notr_omuz = statistics.median(omuzlar) if omuzlar else None
        dzler = [d for _, _, d in ornekler if d is not None]
        self.notr_dz = statistics.mean(dzler) if len(dzler) >= len(ornekler) / 2 else None

    def kalan(self, simdi):
        toplam = YERLESME_SN + KALIBRASYON_SN
        return toplam if self.basla is None else max(toplam - (simdi - self.basla), 0)


class HizTutucu:
    """El geçişinden önceki yumuşatılmış öne-geriyi, iniş geçişi bitene dek tutar."""
    def __init__(self, geri_bak_sn=GERI_BAK_SN, birak_gecikme_sn=BIRAK_GECIKME_SN,
                 sakin_esik=None):
        self.geri_bak_sn = geri_bak_sn
        self.birak_gecikme_sn = birak_gecikme_sn
        self.sakin_esik = sakin_esik
        self.sifirla()

    def sifirla(self):
        self.tutuluyor = False
        self.deger = self.kaynak_zamani = self.null_basla = self.birak_basla = None
        self.tut_basla = None
        self.yeniden_kur_bekle = False
        self.gecmis = deque()
        self.sakin_zamanlar = deque()

    def besle(self, simdi, one, sol_el, sag_el):
        if one is None:
            self.sifirla()
            return None
        self.gecmis.append((simdi, one))
        while self.gecmis and self.gecmis[0][0] < simdi - EL_GECMIS_SN:
            self.gecmis.popleft()
        if (self.sakin_esik is not None and sol_el is not None and sag_el is not None
                and max(sol_el, sag_el) < self.sakin_esik):
            self.sakin_zamanlar.append(simdi)
        while self.sakin_zamanlar and self.sakin_zamanlar[0] < self.gecmis[0][0]:
            self.sakin_zamanlar.popleft()
        if sol_el is None and sag_el is None:
            self.birak_basla = None
            if self.null_basla is None:
                self.null_basla = simdi
            if simdi - self.null_basla >= EL_TUT_NULL_SN:
                self.tutuluyor = False
                self.tut_basla = None
        else:
            self.null_basla = None
            gorunen = [el for el in (sol_el, sag_el) if el is not None]
            if gorunen and all(el < EL_TUT_BIRAK for el in gorunen):
                self.yeniden_kur_bekle = False
                if self.birak_basla is None:
                    self.birak_basla = simdi
                if simdi - self.birak_basla >= self.birak_gecikme_sn:
                    self.tutuluyor = False
                    self.tut_basla = None
            else:
                # Eksik el veya yeniden yükseliş, kesintisiz iniş süresini bozar.
                self.birak_basla = None
        if self.tutuluyor and self.tut_basla is not None and simdi - self.tut_basla >= AZAMI_TUT_SN:
            self.tutuluyor = False
            self.tut_basla = None
            # Donuk hız, kayan hızdan kötü: uzun tutmayı kes, ama kalkık elin
            # hız değişimini hemen yeniden dondurmasına el -1,2 altına inene dek izin verme.
            self.yeniden_kur_bekle = True
        if (not self.tutuluyor and not self.yeniden_kur_bekle
                and any(el is not None and el > EL_TUT_BASLA for el in (sol_el, sag_el))):
            if self.sakin_esik is None:
                hedef = simdi - self.geri_bak_sn
            else:
                hedef = self.sakin_zamanlar[-1] if self.sakin_zamanlar else self.gecmis[0][0]
            self.kaynak_zamani, self.deger = next(
                ((t, o) for t, o in reversed(self.gecmis) if t <= hedef), self.gecmis[0])
            self.tutuluyor = True
            self.tut_basla = simdi
            self.birak_basla = None
        return self.deger if self.tutuluyor else one


class HareketIsleyici:
    """Canlı yayın ve CSV tekrarı için ortak ölçek, yumuşatma ve el tutması."""
    def __init__(self, **tutucu_ayar):
        self.tutucu = HizTutucu(**tutucu_ayar)
        self.sifirla()

    def sifirla(self):
        self.duz_egim = self.duz_one = None
        self.tutucu.sifirla()

    def besle(self, simdi, kal, egim, omuz_orani, sol_el, sag_el):
        if not kal.tamam or egim is None:
            self.sifirla()
            return {"egim": None, "one": None, "tut": False}
        e = GOVDE_OLCEK * (egim - kal.notr_egim)
        self.duz_egim = e if self.duz_egim is None else YUMUSATMA * e + (1 - YUMUSATMA) * self.duz_egim
        if omuz_orani is not None and kal.notr_omuz is not None and kal.notr_omuz >= 0.01:
            r = omuz_orani / kal.notr_omuz - 1
            o = r * ONE_OLCEK if r >= 0 else r * GERI_OLCEK
            self.duz_one = o if self.duz_one is None else YUMUSATMA * o + (1 - YUMUSATMA) * self.duz_one
        else:
            self.duz_one = None
        one = self.tutucu.besle(simdi, self.duz_one, sol_el, sag_el)
        return {"egim": round(self.duz_egim, 2), "one": round(one, 4) if one is not None else None,
                "tut": self.tutucu.tutuluyor}


def main():
    global cv2, mp, np, vision, DONDURME, DONMA_SANIYE, ciz, omuz_egimi
    import cv2
    import mediapipe as mp
    import numpy as np
    from mediapipe.tasks.python import BaseOptions, vision
    from kamera_test import DONDURME, DONMA_SANIYE, MODEL, SonKare, ciz, omuz_egimi

    arg = sys.argv[1] if len(sys.argv) > 1 else "0"
    kaynak = int(arg) if arg.isdigit() else arg
    aci = int(sys.argv[2]) if len(sys.argv) > 2 else (0 if isinstance(kaynak, int) else 90)
    if aci not in DONDURME:
        raise SystemExit(f"Döndürme 0, 90, 180 ya da 270 olmalı, gelen: {aci}")

    secenek = vision.PoseLandmarkerOptions(
        base_options=BaseOptions(model_asset_path=str(MODEL)),
        running_mode=vision.RunningMode.VIDEO,
    )
    soket = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    kamera = SonKare(kaynak)
    olcum_dizini = os.path.join(os.path.dirname(os.path.abspath(__file__)), "olcumler")
    csv_yolu = os.path.join(olcum_dizini, time.strftime("oyun_%Y%m%d_%H%M%S.csv"))
    kayit = None
    csv_yazici = None
    try:
        os.makedirs(olcum_dizini, exist_ok=True)
        kayit = open(csv_yolu, "w", newline="", encoding="utf-8")
        csv_yazici = csv.DictWriter(kayit, fieldnames=("saat", "t", "kalibre", "egim", "one", "donuk", "ham_egim", "ham_dz", "notr_egim", "notr_dz", "ham_omuz", "notr_omuz", "sol_el", "sag_el", "sol_bilek_gor", "sag_bilek_gor", "tut", "ham_omuz_egim"), restval="")
        csv_yazici.writeheader()
        print(f"Ölçüm kaydı: {csv_yolu}", file=sys.stderr)
    except OSError as e:
        print(f"Ölçüm kaydı devre dışı: {e}", file=sys.stderr)
        if kayit is not None:
            kayit.close()
        kayit = csv_yazici = None
    kayit_durumu = {"kayit": kayit, "yazici": csv_yazici, "son_flush": time.monotonic(), "uyari": False}
    try:
        dongu(kamera, secenek, aci, soket, kayit_durumu)
    finally:
        gonder(soket, sinyal_yok(time.monotonic()), kayit_durumu, {"saat": round(time.time(), 3)})  # oyun son eğimde takılı kalmasın
        if kayit_durumu["kayit"] is not None:
            try:
                kayit_durumu["kayit"].close()
            except OSError as e:  # kapanış kalan tamponu yazar; disk hatası kamerayı açık bırakmasın
                print(f"Ölçüm kaydı kapatılamadı: {e}", file=sys.stderr)
        kamera.kapat()
        soket.close()
        cv2.destroyAllWindows()


def gonder(soket, mesaj, kayit_durumu=None, ek=None):
    try:
        soket.sendto(json.dumps(mesaj).encode(), HEDEF)
    except OSError:
        pass  # oyun kapalıyken Windows ICMP "port unreachable" hatası verebilir; kamera tarafı durmasın
    if kayit_durumu is not None and kayit_durumu["yazici"] is not None:
        try:
            kayit_durumu["yazici"].writerow({**mesaj, **(ek or {})})
            if time.monotonic() - kayit_durumu["son_flush"] >= 1.0:
                kayit_durumu["kayit"].flush()
                kayit_durumu["son_flush"] = time.monotonic()
        except OSError as e:
            if not kayit_durumu["uyari"]:
                print(f"Ölçüm kaydı durduruldu: {e}", file=sys.stderr)
                kayit_durumu["uyari"] = True
            try:
                kayit_durumu["kayit"].close()
            except OSError:
                pass
            kayit_durumu["kayit"] = kayit_durumu["yazici"] = None


def sinyal_yok(simdi, kalibre=False, donuk=False):
    return {"t": round(simdi, 3), "kalibre": kalibre, "egim": None, "one": None, "donuk": donuk, "sol_el": None, "sag_el": None, "tut": False}


def dongu(kamera, secenek, aci, soket, kayit_durumu=None):
    son_no, son_ts = 0, 0
    kal = Kalibrasyon()
    isleyici = HareketIsleyici()
    son_sessiz = 0.0
    uyari_gosterildi = False

    with vision.PoseLandmarker.create_from_options(secenek) as model:
        while kamera.calisiyor:
            no, kare, kare_zamani = kamera.al()
            if kare is None or no == son_no:
                simdi = time.monotonic()
                if simdi - kare_zamani > DONUK_ESIK_SN and simdi - son_sessiz >= SESSIZ_ARALIK_SN:
                    # Akış dondu: susmak oyunu son değerde bırakır, açıkça "sinyal yok" de.
                    gonder(soket, sinyal_yok(simdi, kal.tamam, donuk=True), kayit_durumu, {"saat": round(time.time(), 3)})
                    son_sessiz = simdi
                    isleyici.sifirla()
                if simdi - kare_zamani > DONMA_SANIYE:
                    if not uyari_gosterildi:
                        bos = np.zeros((480, 640, 3), np.uint8)
                        cv2.putText(bos, "KARE GELMIYOR", (10, 100), cv2.FONT_HERSHEY_SIMPLEX, 1.0, (0, 0, 255), 3)
                        cv2.imshow(PENCERE, bos)
                        uyari_gosterildi = True
                t = cv2.waitKey(1) & 0xFF
                if t in (ord("q"), 27):
                    return
                if t == ord("c"):
                    kal.sifirla()
                    isleyici.sifirla()
                continue
            son_no = no
            uyari_gosterildi = False
            simdi = time.monotonic()

            if DONDURME[aci] is not None:
                kare = cv2.rotate(kare, DONDURME[aci])
            kare = cv2.flip(kare, 1)
            ts = max(int(simdi * 1000), son_ts + 1)
            son_ts = ts
            rgb = mp.Image(image_format=mp.ImageFormat.SRGB, data=cv2.cvtColor(kare, cv2.COLOR_BGR2RGB))
            sonuc = model.detect_for_video(rgb, ts)
            if sonuc.pose_landmarks:
                ciz(kare, sonuc.pose_landmarks[0])

            egim, omuz_px, dz = ham_olcum(sonuc, kare.shape[1], kare.shape[0])
            ham_omuz_egim = (omuz_egimi(sonuc.pose_landmarks[0], kare.shape[1], kare.shape[0])
                             if omuz_px is not None else None)
            if ham_omuz_egim is not None and not math.isfinite(ham_omuz_egim):
                ham_omuz_egim = None
            # NaN/sonsuz değer oyuna gitmesin (JSON'da "NaN" olur, oyunda konumu bozar).
            if egim is not None and not math.isfinite(egim):
                egim, omuz_px, dz = None, None, None
            if omuz_px is not None and not math.isfinite(omuz_px):
                omuz_px = None
            if dz is not None and not math.isfinite(dz):
                dz = None
            kal.besle(simdi, egim, omuz_px, dz)

            mesaj = sinyal_yok(simdi, kal.tamam)
            sol_el, sag_el, sol_gor, sag_gor = el_olcum(sonuc, kare.shape[1], kare.shape[0], egim, omuz_px)
            mesaj.update(sol_el=sol_el, sag_el=sag_el)
            mesaj.update(isleyici.besle(simdi, kal, egim, omuz_px, sol_el, sag_el))
            ek = {
                "saat": round(time.time(), 3),
                "ham_egim": round(egim, 2) if egim is not None else None,
                "ham_dz": round(dz, 4) if dz is not None else None,
                "notr_egim": round(kal.notr_egim, 2) if kal.notr_egim is not None else None,
                "notr_dz": round(kal.notr_dz, 4) if kal.notr_dz is not None else None,
                "ham_omuz": round(omuz_px, 5) if omuz_px is not None else None,
                "notr_omuz": round(kal.notr_omuz, 5) if kal.notr_omuz is not None else None,
                "sol_bilek_gor": sol_gor,
                "sag_bilek_gor": sag_gor,
                "ham_omuz_egim": round(ham_omuz_egim, 2) if ham_omuz_egim is not None else None,
            }
            gonder(soket, mesaj, kayit_durumu, ek)

            if not kal.tamam:
                durum = (f"KALIBRASYON: oynayacagin gibi dur, kipirdama  {kal.kalan(simdi):.0f}" if egim is not None
                         else "KALIBRASYON: kalcalar gorunsun" if omuz_px is not None else "KALIBRASYON: omuzlar gorunsun")
            elif mesaj["egim"] is None:
                durum = "kalcalar gorunsun" if omuz_px is not None else "kisi / omuz gorunmuyor"
            else:
                if mesaj["one"] is not None:
                    one = f"{mesaj['one'] * 100:+.1f} cm"
                elif kal.notr_omuz is None or kal.notr_omuz < 0.01:
                    one = "omuz notru yok (c: yeniden kalibre)"
                else:
                    one = "omuz yok"
                durum = f"egim {mesaj['egim']:+.0f} derece   one {one}"
            cv2.putText(kare, durum, (10, 35), cv2.FONT_HERSHEY_SIMPLEX, 0.8, (0, 255, 255), 2)
            cv2.putText(kare, "c: yeniden kalibre   q: cik", (10, 65), cv2.FONT_HERSHEY_SIMPLEX, 0.55, (255, 255, 255), 1)
            cv2.imshow(PENCERE, kare)

            t = cv2.waitKey(1) & 0xFF
            if t in (ord("q"), 27):
                return
            if t == ord("c"):
                kal.sifirla()
                isleyici.sifirla()


if __name__ == "__main__":
    main()
