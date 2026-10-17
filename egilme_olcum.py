"""Öne / geriye eğilme ölçümü: hangi gösterge yönü ayırıyor?

Ekrandaki yönergeyi izle (dik dur → öne eğil → dik dur → geriye eğil, iki tur). Her karede
üç aday gösterge kaydedilir, sonunda CSV dosyası `olcumler/` altına yazılır:
  govde  : omuz ortası ile kalça ortası arası piksel uzunluğu (Batın'ın fikri; iki yönde de kısalır)
  omuz   : iki omuz arası piksel uzunluğu (kameraya yaklaşınca büyür)
  kulak  : iki kulak arası piksel uzunluğu (aynı mantık, baş için)
  dz     : MediaPipe'ın metre cinsinden derinlik tahmininde omuz ortası - kalça ortası
           (negatif = omuzlar kalçadan kameraya daha yakın; 2026-10-17 verisinde öne eğilince azaldı)
  egim   : omuz çizgisinin yataydan sapması, derece (oyundaki yan eğim)
  donme  : MediaPipe'ın 11 - 12 numaralı omuz z farkı, metre (gövde kameraya göre dönerse değişir).
           Görüntü aynalandığı için etiketler kişinin bedenine göre ters olabilir: işaretine değil,
           sağa ve sola eğilmede değişip değişmediğine bak.
Ekranda ilk "dik dur" evresine göre oranlar gösterilir.

--yan ile protokol yana eğilmeye döner (dik → sağa → dik → sola): yana yatınca dz'nin
(oyundaki "öne" sinyali) ne kadar kaydığını ölçmek için. 2026-10-17 ilk oyun denemesinde
Batın yana yatınca bazen hızlandığını / yavaşladığını gördü.

--tam yaklaşık 2 dakikalık protokoldür; her kaydedilen karede MediaPipe'ın 33 noktasını
ham olarak saklar. Amaç, yeni göstergeleri aynı kayıtlarda sonradan karşılaştırmaktır.

Kullanım:
    .venv\\Scripts\\python.exe egilme_olcum.py http://192.168.1.144:4747/video
    .venv\\Scripts\\python.exe egilme_olcum.py http://192.168.1.144:4747/video --yan
    .venv\\Scripts\\python.exe egilme_olcum.py http://192.168.1.144:4747/video --tam
    .venv\\Scripts\\python.exe egilme_olcum.py http://192.168.1.144:4747/video --el
--el el kaldirma protokolunu calistirir ve tam mod gibi 33 noktayi kaydeder.
Kalçalar kadrajda görünmeli. Çıkış: q ya da Esc (o ana kadarki kayıt yine yazılır).
"""

import csv
import math
import sys
import time
from datetime import datetime
from pathlib import Path

import cv2
import mediapipe as mp
import numpy as np
from mediapipe.tasks.python import BaseOptions, vision

from kamera_test import DONDURME, DONMA_SANIYE, MIN_GORUNURLUK, MODEL, SonKare, ciz, omuz_egimi

KULAKLAR, OMUZLAR, KALCALAR = (7, 8), (11, 12), (23, 24)
PENCERE = "Fizyo-Oyun egilme olcumu"
# (ekran yazısı, süre sn, CSV etiketi). İlk "dik" evresi nötr duruş (kalibrasyon) olur.
# 2026-10-17 deneme 1: 3 sn'lik dik evrenin sonunda kişi erkenden eğildi, kalibrasyon kirlendi.
EVRELER = [("HAZIRLAN - yerlesip kipirdama", 6, "hazir")] + [
    ("DIK DUR", 4, "dik"),
    ("ONE EGIL", 5, "one"),
    ("DIK DUR", 4, "dik"),
    ("GERIYE EGIL", 5, "geri"),
] * 2
YAN_EVRELER = [EVRELER[0]] + [
    ("DIK DUR", 4, "dik"),
    ("SAGA EGIL (sadece yana)", 5, "sag"),
    ("DIK DUR", 4, "dik"),
    ("SOLA EGIL (sadece yana)", 5, "sol"),
] * 2
TAM_EVRELER = [EVRELER[0]] + [
    ("DIK DUR", 4, "dik"),
    ("ONE EGIL", 5, "one"),
    ("DIK DUR", 4, "dik"),
    ("GERIYE EGIL", 5, "geri"),
    ("DIK DUR", 4, "dik"),
    ("SAGA EGIL", 5, "sag"),
    ("DIK DUR", 4, "dik"),
    ("SOLA EGIL", 5, "sol"),
    ("DIK DUR", 4, "dik"),
    ("SAGA EGIL + ONE EGIL", 5, "sag_one"),
    ("DIK DUR", 4, "dik"),
    ("SOLA EGIL + ONE EGIL", 5, "sol_one"),
] * 2
EL_EVRELER = [EVRELER[0]] + [
    ("DIK DUR", 4, "dik"),
    ("SOL ELINI KALDIR (bas hizasina)", 5, "sol_el"),
    ("DIK DUR", 4, "dik"),
    ("SAG ELINI KALDIR (bas hizasina)", 5, "sag_el"),
    ("DIK DUR", 4, "dik"),
    ("ONE EGIL + SOL ELINI KALDIR", 5, "one_sol_el"),
    ("DIK DUR", 4, "dik"),
    ("ONE EGIL + SAG ELINI KALDIR", 5, "one_sag_el"),
] * 2
GECIS_SN = 1.0  # her evrenin ilk ve son bu kadar saniyesi nötre katılmaz (yerleşme / erken davranma)


def uzaklik(noktalar, i, j, g, y):
    a, b = noktalar[i], noktalar[j]
    return math.hypot((a.x - b.x) * g, (a.y - b.y) * y)


def orta(noktalar, cift, g, y):
    a, b = (noktalar[i] for i in cift)
    return (a.x + b.x) / 2 * g, (a.y + b.y) / 2 * y


def olc(sonuc, g, y):
    """Bir karenin göstergeleri; gereken noktalar görünmüyorsa None."""
    if not sonuc.pose_landmarks or not sonuc.pose_world_landmarks:
        return None
    n = sonuc.pose_landmarks[0]
    if min(n[i].visibility or 0 for i in OMUZLAR + KALCALAR) < MIN_GORUNURLUK:
        return None
    (ox, oy), (kx, ky) = orta(n, OMUZLAR, g, y), orta(n, KALCALAR, g, y)
    w = sonuc.pose_world_landmarks[0]
    dz = (w[11].z + w[12].z) / 2 - (w[23].z + w[24].z) / 2
    kulak_gorunur = min(n[i].visibility or 0 for i in KULAKLAR) >= MIN_GORUNURLUK
    return {
        "govde": math.hypot(ox - kx, oy - ky),
        "omuz": uzaklik(n, *OMUZLAR, g, y),
        "kulak": uzaklik(n, *KULAKLAR, g, y) if kulak_gorunur else None,
        "dz": dz,
        "egim": omuz_egimi(n, g, y),
        "donme": w[11].z - w[12].z,
    }


def ortalama(satirlar, alan):
    degerler = [s[alan] for s in satirlar if s[alan] is not None]
    return sum(degerler) / len(degerler) if degerler else None


def main():
    yan = "--yan" in sys.argv
    tam = "--tam" in sys.argv
    el = "--el" in sys.argv
    argumanlar = [a for a in sys.argv[1:] if a not in ("--yan", "--tam", "--el")]
    arg = argumanlar[0] if argumanlar else "0"
    kaynak = int(arg) if arg.isdigit() else arg
    aci = int(argumanlar[1]) if len(argumanlar) > 1 else (0 if isinstance(kaynak, int) else 90)
    if aci not in DONDURME:
        raise SystemExit(f"Döndürme 0, 90, 180 ya da 270 olmalı, gelen: {aci}")

    secenek = vision.PoseLandmarkerOptions(
        base_options=BaseOptions(model_asset_path=str(MODEL)),
        running_mode=vision.RunningMode.VIDEO,
    )
    kayit = []
    kamera = SonKare(kaynak)
    try:
        evreler = EL_EVRELER if el else TAM_EVRELER if tam else YAN_EVRELER if yan else EVRELER
        olcum_dongusu(kamera, secenek, aci, kayit, evreler, tam=tam or el)
    finally:
        yaz(kayit, "el" if el else "tam" if tam else "yan" if yan else "egilme")  # önce kayıt: kapanışta bir şey patlasa da veri kaybolmasın
        kamera.kapat()
        cv2.destroyAllWindows()


def olcum_dongusu(kamera, secenek, aci, kayit, evreler, tam=False):
    son_no, son_ts = 0, 0
    notr = None
    evre_no, evre_basi = 0, None  # ilk kare gelince saat başlar

    with vision.PoseLandmarker.create_from_options(secenek) as model:
        while kamera.calisiyor and evre_no < len(evreler):
            no, kare, kare_zamani = kamera.al()
            if kare is None or no == son_no:
                if son_no == 0 and time.monotonic() - kare_zamani > DONMA_SANIYE:
                    # Hiç kare gelmezse pencere açılmaz, q da çalışmaz; yer tutucu göster.
                    bos = np.zeros((480, 640, 3), np.uint8)
                    cv2.putText(bos, "KARE GELMIYOR", (10, 100), cv2.FONT_HERSHEY_SIMPLEX, 1.0, (0, 0, 255), 3)
                    cv2.imshow(PENCERE, bos)
                if cv2.waitKey(1) & 0xFF in (ord("q"), 27):
                    return
                continue
            son_no = no
            simdi = time.monotonic()
            if evre_basi is None:
                evre_basi = simdi
            yazi, sure, etiket = evreler[evre_no]
            gecen = simdi - evre_basi

            if DONDURME[aci] is not None:
                kare = cv2.rotate(kare, DONDURME[aci])
            kare = cv2.flip(kare, 1)
            ts = max(int(simdi * 1000), son_ts + 1)
            son_ts = ts
            rgb = mp.Image(image_format=mp.ImageFormat.SRGB, data=cv2.cvtColor(kare, cv2.COLOR_BGR2RGB))
            sonuc = model.detect_for_video(rgb, ts)
            g, y = kare.shape[1], kare.shape[0]
            deger = olc(sonuc, g, y)
            if sonuc.pose_landmarks:
                ciz(kare, sonuc.pose_landmarks[0])

            if deger is not None:
                satir = {"t": round(simdi, 3), "evre": evre_no, "etiket": etiket,
                         "evrede_sn": round(gecen, 2), **deger}
                if tam:
                    satir.update({"gen": g, "yuk": y})
                    for i, nokta in enumerate(sonuc.pose_landmarks[0]):
                        satir.update({f"g{i}_x": nokta.x, f"g{i}_y": nokta.y, f"g{i}_z": nokta.z,
                                      f"g{i}_v": nokta.visibility})
                    for i, nokta in enumerate(sonuc.pose_world_landmarks[0]):
                        satir.update({f"w{i}_x": nokta.x, f"w{i}_y": nokta.y, f"w{i}_z": nokta.z})
                kayit.append(satir)

            if deger is None:
                durum = "kalca/omuz gorunmuyor"
            elif notr is None:
                durum = f"govde {deger['govde']:.0f}px  omuz {deger['omuz']:.0f}px  dz {deger['dz']:+.2f}m"
            else:
                parcalar = [f"govde {deger['govde'] / notr['govde']:.2f}", f"omuz {deger['omuz'] / notr['omuz']:.2f}"]
                if deger["kulak"] is not None and notr["kulak"]:
                    parcalar.append(f"kulak {deger['kulak'] / notr['kulak']:.2f}")
                parcalar.append(f"dz {deger['dz'] - notr['dz']:+.3f}")
                parcalar.append(f"egim {deger['egim'] - notr['egim']:+.0f}")
                durum = "  ".join(parcalar)

            cv2.putText(kare, f"{yazi}  {max(sure - gecen, 0):.0f}", (10, 40), cv2.FONT_HERSHEY_SIMPLEX, 1.1, (0, 255, 255), 3)
            cv2.putText(kare, durum, (10, 75), cv2.FONT_HERSHEY_SIMPLEX, 0.6, (255, 255, 255), 2)
            cv2.imshow(PENCERE, kare)
            if cv2.waitKey(1) & 0xFF in (ord("q"), 27):
                return

            if gecen >= sure:
                if etiket == "dik" and notr is None:
                    # Evrenin ilk saniyesi geçiş (kişi henüz yerleşmemiş), nötre katılmaz.
                    satirlar = [s for s in kayit if s["evre"] == evre_no and GECIS_SN <= s["evrede_sn"] <= sure - GECIS_SN]
                    if satirlar:
                        notr = {a: ortalama(satirlar, a) for a in ("govde", "omuz", "kulak", "dz", "egim", "donme")}
                evre_no, evre_basi = evre_no + 1, evre_basi + sure  # aşan süre sonraki evreden düşülür


def yaz(kayit, onek):
    if not kayit:
        print("Kayıt yok, dosya yazılmadı.", file=sys.stderr)
        return
    klasor = Path(__file__).parent / "olcumler"
    klasor.mkdir(exist_ok=True)
    yol = klasor / f"{onek}_{datetime.now():%Y%m%d_%H%M%S}.csv"
    with open(yol, "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=list(kayit[0]))
        w.writeheader()
        w.writerows(kayit)
    print(f"Kaydedildi: {yol} ({len(kayit)} kare)", file=sys.stderr)


if __name__ == "__main__":
    main()
