"""Kamera denemesi: kameradan canlı iskelet çizer, omuz eğimini ve FPS'i gösterir.

Kullanım:
    .venv\\Scripts\\python.exe kamera_test.py                          # bilgisayara bağlı ilk kamera
    .venv\\Scripts\\python.exe kamera_test.py http://192.168.1.5:4747/video   # DroidCam adresi
    .venv\\Scripts\\python.exe kamera_test.py http://192.168.1.5:4747/video 90  # görüntüyü 90° döndür

Tuşlar (pencere seçiliyken): r görüntüyü saat yönünde 90° döndürür, q ya da Esc çıkar.
"""

import math
import os
import sys
import threading
import time
from pathlib import Path

import cv2
import mediapipe as mp
import numpy as np
from mediapipe.tasks.python import BaseOptions, vision

MODEL = Path(__file__).parent / "models" / "pose_landmarker_full.task"
OMUZLAR = (11, 12)  # MediaPipe: sol omuz, sağ omuz
ILK_GOVDE_NOKTASI = 11  # 0-10 yüz noktaları: hesapta kalır (baş eğerek hile ileride buradan ayrılır), çizilmez
ORTA_ESIK_DERECE = 5  # bu eğimin altı "ORTA" sayılır; klinik değer değil, deneme için
MIN_GORUNURLUK = 0.5  # omuz bundan az görünürse eğim gösterilmez
DONMA_SANIYE = 1.0  # bu kadar süre yeni kare gelmezse ekrana uyarı basılır
PENCERE = "Fizyo-Oyun kamera testi"
DONDURME = {
    0: None,
    90: cv2.ROTATE_90_CLOCKWISE,
    180: cv2.ROTATE_180,
    270: cv2.ROTATE_90_COUNTERCLOCKWISE,
}


class SonKare:
    """Kameradan arka planda okur, sadece en son kareyi tutar.

    Ağ kamerasında (DroidCam) OpenCV kareleri biriktirir; okumak işlemekten yavaş kalırsa
    görüntü saniyeler geriden gelir. Eski kareleri atmak gecikmeyi sabit tutar.
    """

    def __init__(self, kaynak):
        if isinstance(kaynak, int) and os.name == "nt":
            self.cap = cv2.VideoCapture(kaynak, cv2.CAP_DSHOW)
            if not self.cap.isOpened():
                self.cap.release()
                self.cap = cv2.VideoCapture(kaynak)
        else:
            self.cap = cv2.VideoCapture(kaynak)
        if not self.cap.isOpened():
            self.cap.release()
            print(f"Kamera açılamadı: {kaynak!r}; bağlantıyı ve kamera iznini kontrol edin.", file=sys.stderr)
            raise SystemExit(2)
        self.kare = None
        self.no = 0
        self.zaman = time.monotonic()  # son karenin geldiği an; donmuş akışı görünür kılmak için
        self.kilit = threading.Lock()
        self.calisiyor = True
        self.is_parcacigi = threading.Thread(target=self._oku, daemon=True)
        self.is_parcacigi.start()

    def _oku(self):
        # VideoCapture iş parçacığı güvenli değil: read() ile release() aynı anda çalışmasın diye
        # kamerayı sadece bu iş parçacığı serbest bırakır.
        try:
            while self.calisiyor:
                ok, kare = self.cap.read()
                if not ok:
                    print("Kameradan kare gelmedi, bağlantı koptu.", file=sys.stderr)
                    return
                with self.kilit:
                    self.kare = kare
                    self.no += 1
                    self.zaman = time.monotonic()
        finally:
            self.calisiyor = False
            self.cap.release()

    def al(self):
        with self.kilit:
            return self.no, self.kare, self.zaman

    def kapat(self):
        self.calisiyor = False
        self.is_parcacigi.join(timeout=2)
        if self.is_parcacigi.is_alive():
            print("Kamera serbest bırakılamadı (okuma takılı kaldı).", file=sys.stderr)


def omuz_egimi(noktalar, genislik, yukseklik):
    """Ekranda soldaki omuzdan sağdakine çizgi; yataydan sapması derece cinsinden.

    Pozitif: ekranın sağ tarafı aşağıda (kişi ekranda sağa eğilmiş).
    """
    a, b = (noktalar[i] for i in OMUZLAR)
    (x1, y1), (x2, y2) = sorted([(a.x * genislik, a.y * yukseklik), (b.x * genislik, b.y * yukseklik)])
    return math.degrees(math.atan2(y2 - y1, x2 - x1))


def ciz(kare, noktalar):
    y, x = kare.shape[:2]
    for c in vision.PoseLandmarksConnections.POSE_LANDMARKS:
        if min(c.start, c.end) < ILK_GOVDE_NOKTASI:
            continue
        p, q = noktalar[c.start], noktalar[c.end]
        cv2.line(kare, (int(p.x * x), int(p.y * y)), (int(q.x * x), int(q.y * y)), (0, 255, 0), 2)
    for n in noktalar[ILK_GOVDE_NOKTASI:]:
        cv2.circle(kare, (int(n.x * x), int(n.y * y)), 3, (0, 0, 255), -1)


def main():
    arg = sys.argv[1] if len(sys.argv) > 1 else "0"
    kaynak = int(arg) if arg.isdigit() else arg
    # Telefon (DroidCam) dik tutulunca görüntü yan geliyor; 2026-10-17 denemesinde 90 doğru çıktı.
    varsayilan_aci = 0 if isinstance(kaynak, int) else 90
    aci = int(sys.argv[2]) if len(sys.argv) > 2 else varsayilan_aci
    if aci not in DONDURME:
        raise SystemExit(f"Döndürme 0, 90, 180 ya da 270 olmalı, gelen: {aci}")

    if not MODEL.exists():
        raise SystemExit(f"Model yok: {MODEL}")
    secenek = vision.PoseLandmarkerOptions(
        base_options=BaseOptions(model_asset_path=str(MODEL)),
        running_mode=vision.RunningMode.VIDEO,
    )
    kamera = SonKare(kaynak)
    try:
        calistir(kamera, secenek, aci)
    finally:
        kamera.kapat()
        cv2.destroyAllWindows()


def tus(aci):
    """Tuşa bakar. Çıkılacaksa None, değilse (r'ye basıldıysa güncellenmiş) açıyı döndürür."""
    t = cv2.waitKey(1) & 0xFF
    if t in (ord("q"), 27):
        return None
    if t == ord("r"):
        aci = (aci + 90) % 360
        print(f"Döndürme: {aci} (sonraki sefer komutun sonuna {aci} yaz)", file=sys.stderr)
    return aci


def calistir(kamera, secenek, aci):
    son_no, son_ts, fps, t_once = 0, 0, None, None
    son_gosterilen = None
    uyari_gosterildi = False

    with vision.PoseLandmarker.create_from_options(secenek) as model:
        while kamera.calisiyor:
            no, kare, kare_zamani = kamera.al()
            if kare is None or no == son_no:
                # Ağ akışı donarsa read() hata vermeden bekleyebilir; ekranda son kare canlıymış
                # gibi kalmasın. İlk kare hiç gelmezse de pencere açılsın ki q çalışsın.
                if not uyari_gosterildi and time.monotonic() - kare_zamani > DONMA_SANIYE:
                    uyari = son_gosterilen.copy() if son_gosterilen is not None else np.zeros((480, 640, 3), np.uint8)
                    cv2.putText(uyari, "KARE GELMIYOR", (10, 100), cv2.FONT_HERSHEY_SIMPLEX, 1.0, (0, 0, 255), 3)
                    cv2.imshow(PENCERE, uyari)
                    uyari_gosterildi = True
                aci = tus(aci)
                if aci is None:
                    break
                continue
            son_no = no
            uyari_gosterildi = False

            if DONDURME[aci] is not None:
                kare = cv2.rotate(kare, DONDURME[aci])
            kare = cv2.flip(kare, 1)  # ayna: sağa eğilince ekranda da sağa gitsin
            ts = max(int(time.monotonic() * 1000), son_ts + 1)  # VIDEO modu artan zaman ister
            son_ts = ts
            rgb = mp.Image(image_format=mp.ImageFormat.SRGB, data=cv2.cvtColor(kare, cv2.COLOR_BGR2RGB))
            sonuc = model.detect_for_video(rgb, ts)

            if not sonuc.pose_landmarks:
                metin = "kisi yok"
            else:
                noktalar = sonuc.pose_landmarks[0]
                ciz(kare, noktalar)
                # Kadraj dışındaki omuzun yerini model yine tahmin eder; uydurma eğim gösterme.
                if min(noktalar[i].visibility or 0 for i in OMUZLAR) < MIN_GORUNURLUK:
                    metin = "omuz gorunmuyor"
                else:
                    egim = round(omuz_egimi(noktalar, kare.shape[1], kare.shape[0]))
                    yon = "ORTA" if abs(egim) < ORTA_ESIK_DERECE else ("SAG" if egim > 0 else "SOL")
                    metin = f"egim {egim:+d} derece  {yon}"

            simdi = time.monotonic()
            if t_once is not None:
                anlik = 1 / max(simdi - t_once, 1e-6)
                fps = anlik if fps is None else 0.9 * fps + 0.1 * anlik
            t_once = simdi
            cv2.putText(kare, metin, (10, 30), cv2.FONT_HERSHEY_SIMPLEX, 0.9, (255, 255, 255), 2)
            alt = f"donus {aci}  (r: dondur, q: cik)"
            if fps is not None:
                alt = f"{fps:.0f} fps  " + alt
            cv2.putText(kare, alt, (10, 60), cv2.FONT_HERSHEY_SIMPLEX, 0.6, (255, 255, 255), 2)
            cv2.imshow(PENCERE, kare)
            son_gosterilen = kare
            aci = tus(aci)
            if aci is None:
                break


if __name__ == "__main__":
    main()
