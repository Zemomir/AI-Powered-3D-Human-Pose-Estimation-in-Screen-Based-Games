"""Kameradan gövde hareketini okuyup oyuna (Unity) UDP ile gönderir.
Oyun içi hissi ölçüm verisiyle karşılaştırmak için gönderilen mesajları CSV'ye kaydeder.

Her karede 127.0.0.1:5065'e tek satırlık JSON gider; CSV kaydı saat,t,kalibre,egim,one,donuk,ham_egim,ham_dz,notr_egim,notr_dz,ham_omuz,notr_omuz sütunlarını içerir:
    {"t": 12.345, "kalibre": true, "egim": 8.2, "one": 0.031}
  egim : yan eğim, derece; + = kişi sağına eğik (nötr duruşa göre)
  one  : öne/geriye eğilme, omuz genişliği oranından metre eşdeğeri; + = öne, nötre göre.
  donuk: true ise telefondan kare gelmiyor (Python çalışıyor ama akış dondu).
  Kişi ya da omuzlar görünmüyorsa egim ve one null olur; oyun bunu "sinyal yok" diye ele almalı.
  Kamera donarsa her SESSIZ_ARALIK_SN'de null mesaj, çıkışta da son bir null mesaj gider. Yine de
  oyun ~0,5 sn mesaj almazsa kendisi "sinyal yok" saymalı (betik çökerse null gönderemez).

Kalibrasyon: kişi göründükten sonra YERLESME_SN atlanır (yerleşme, c'ye basan elin dönüşü),
sonraki KALIBRASYON_SN'nin ortalaması nötr sayılır (yan eğim de: çocuğun dinlenme duruşu asimetrik olabilir). c tuşu yeniden kalibre eder.

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

import cv2
import mediapipe as mp
import numpy as np
from mediapipe.tasks.python import BaseOptions, vision

from egilme_olcum import KALCALAR, OMUZLAR
from kamera_test import DONDURME, DONMA_SANIYE, MIN_GORUNURLUK, MODEL, SonKare, ciz, omuz_egimi

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
PENCERE = "Fizyo-Oyun hareket yayini"


def ham_olcum(sonuc, g, y):
    """(egim, omuz_px, dz) döndürür; omuzlar yoksa hepsi None. dz: omuz ortası z - kalça ortası z, metre."""
    if not sonuc.pose_landmarks:
        return None, None, None
    n = sonuc.pose_landmarks[0]
    if min(n[i].visibility or 0 for i in OMUZLAR) < MIN_GORUNURLUK:
        return None, None, None
    egim = omuz_egimi(n, g, y)
    omuz_px = math.hypot((n[11].x - n[12].x) * g, (n[11].y - n[12].y) * y)
    dz = None
    if sonuc.pose_world_landmarks and min(n[i].visibility or 0 for i in KALCALAR) >= MIN_GORUNURLUK:
        w = sonuc.pose_world_landmarks[0]
        dz = (w[11].z + w[12].z) / 2 - (w[23].z + w[24].z) / 2
    return egim, omuz_px, dz


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
            self.notr_egim = sum(e for e, _, _ in self.ornekler) / len(self.ornekler)
            omuzlar = [o for _, o, _ in self.ornekler if o is not None]
            self.notr_omuz = statistics.median(omuzlar) if omuzlar else None
            dzler = [d for _, _, d in self.ornekler if d is not None]
            # Kalçalar kalibrasyonun yarısından azında göründüyse nötr dz güvenilmez.
            self.notr_dz = sum(dzler) / len(dzler) if len(dzler) >= len(self.ornekler) / 2 else None

    def kalan(self, simdi):
        toplam = YERLESME_SN + KALIBRASYON_SN
        return toplam if self.basla is None else max(toplam - (simdi - self.basla), 0)


def main():
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
        csv_yazici = csv.DictWriter(kayit, fieldnames=("saat", "t", "kalibre", "egim", "one", "donuk", "ham_egim", "ham_dz", "notr_egim", "notr_dz", "ham_omuz", "notr_omuz"), restval="")
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
    return {"t": round(simdi, 3), "kalibre": kalibre, "egim": None, "one": None, "donuk": donuk}


def dongu(kamera, secenek, aci, soket, kayit_durumu=None):
    son_no, son_ts = 0, 0
    kal = Kalibrasyon()
    duz_egim = duz_one = None
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
                    duz_egim = duz_one = None
                if simdi - kare_zamani > DONMA_SANIYE:
                    if not uyari_gosterildi:
                        bos = np.zeros((480, 640, 3), np.uint8)
                        cv2.putText(bos, "KARE GELMIYOR", (10, 100), cv2.FONT_HERSHEY_SIMPLEX, 1.0, (0, 0, 255), 3)
                        cv2.imshow(PENCERE, bos)
                        uyari_gosterildi = True
                t = cv2.waitKey(1) & 0xFF
                if t in (ord("q"), 27):
                    return
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
            # NaN/sonsuz değer oyuna gitmesin (JSON'da "NaN" olur, oyunda konumu bozar).
            if egim is not None and not math.isfinite(egim):
                egim, omuz_px, dz = None, None, None
            if omuz_px is not None and not math.isfinite(omuz_px):
                omuz_px = None
            if dz is not None and not math.isfinite(dz):
                dz = None
            kal.besle(simdi, egim, omuz_px, dz)

            mesaj = sinyal_yok(simdi, kal.tamam)
            if kal.tamam and egim is not None:
                e = egim - kal.notr_egim
                duz_egim = e if duz_egim is None else YUMUSATMA * e + (1 - YUMUSATMA) * duz_egim
                mesaj["egim"] = round(duz_egim, 2)
                if omuz_px is not None and kal.notr_omuz is not None and kal.notr_omuz >= 1:
                    r = omuz_px / kal.notr_omuz - 1
                    o = r * ONE_OLCEK if r >= 0 else r * GERI_OLCEK
                    duz_one = o if duz_one is None else YUMUSATMA * o + (1 - YUMUSATMA) * duz_one
                    mesaj["one"] = round(duz_one, 4)
                else:
                    duz_one = None
            else:
                # Sinyal kopunca eski değer yumuşatmada sürüklenmesin.
                duz_egim = duz_one = None
            ek = {
                "saat": round(time.time(), 3),
                "ham_egim": round(egim, 2) if egim is not None else None,
                "ham_dz": round(dz, 4) if dz is not None else None,
                "notr_egim": round(kal.notr_egim, 2) if kal.notr_egim is not None else None,
                "notr_dz": round(kal.notr_dz, 4) if kal.notr_dz is not None else None,
                "ham_omuz": round(omuz_px, 2) if omuz_px is not None else None,
                "notr_omuz": round(kal.notr_omuz, 2) if kal.notr_omuz is not None else None,
            }
            gonder(soket, mesaj, kayit_durumu, ek)

            if not kal.tamam:
                durum = f"KALIBRASYON: oynayacagin gibi rahat otur  {kal.kalan(simdi):.0f}" if egim is not None else "KALIBRASYON: omuzlar gorunsun"
            elif mesaj["egim"] is None:
                durum = "kisi / omuz gorunmuyor"
            else:
                if mesaj["one"] is not None:
                    one = f"{mesaj['one'] * 100:+.1f} cm"
                elif kal.notr_omuz is None or kal.notr_omuz < 1:
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
                duz_egim = duz_one = None


if __name__ == "__main__":
    main()
