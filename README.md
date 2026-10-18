# Fizyo-Oyun

Kamera kontrollü fizyoterapi oyunu, hemipleji tanılı çocuklar için geliştirilmiştir. TÜBİTAK 2209-B projesidir. Unity oyunu, MediaPipe kullanan Python kamera köprüsünden poz bilgisi alır.

## Nasıl oynanır?

- Gövdeni sağa veya sola eğerek motosikleti döndür.
- Öne eğilerek hızlan, geriye eğilerek yavaşla.
- Sarı-turuncu tişörtlü seyirci yaklaşınca, bulunduğu taraftaki elini omuz hizasının üstüne kaldırıp çak.
- Yol üzerindeki yıldızları topla.

## Laptopta kurulum

1. Releases'tan `FizyoOyun-<sürüm>.zip` dosyasını indir.
2. ZIP dosyasına sağ tıkla ve **Tümünü ayıkla** seçeneğini seç.
3. `FizyoOyun\FizyoOyun.exe` dosyasına çift tıkla. İnternet gerekmez.

Windows “PC'niz korundu” uyarısı çıkarsa **Ek bilgi → Yine de çalıştır** seç (uygulama imzasızdır). Python kurulumu gerekmez.

## Kamera ve konumlanma

Kamera otomatik bulunur: oyun laptop kameralarını sırayla dener ve seni gören kamerayı (ve görüntünün doğru dönüşünü) seçer. Laptop kamerası yoksa menüdeki **Telefon adresi** (DroidCam `IP:port`) kullanılır. Yanlış kamera seçildiyse ya da sonradan kamera taktıysan **Yeniden ara**'ya bas. Kameradan 2-3 metre uzakta, ayakta dur; omuzların ve kalçaların görüntüde olsun. Masaya oturunca kalçalar görünmez ve direksiyon kontrolü çalışmaz; menüdeki durum satırı bunu "Kalçaların görünmüyor" diye söyler.

Oyun ekranın kendi çözünürlüğünde tam ekran açılır; **F11** pencere ile tam ekran arasında geçer.

## Tuşlar

- `Esc`: duraklat
- `R`: baştan başlat
- `C`: duruşu sıfırla
- `H`: ayrıntıları göster/gizle
- `M`: sesi aç/kapat
- `+` / `-`: hız hassasiyeti
- `1` / `2`: dönüş hassasiyeti
- `K`: klavye modunu aç/kapat (`Q` / `E`: sol/sağ el)
- `Y`: yumuşatmayı aç/kapat

## Geliştiriciler için

Unity sürümü: **6000.3**. Sahne kodla kurulur ve derlenir:

```powershell
"<Unity.exe>" -batchmode -quit -projectPath oyun -executeMethod SahneKur.Derle -logFile unity_derle.log
```

Geliştirme için depo kökünde `kurulum.bat`, ardından `baslat.bat` çalıştır. Paketleme komutu: `powershell -File paketle.ps1`. Pilot çalıştırma: `FizyoOyun.exe -otopilot -otopilotOfset 0`.

## Veri gizliliği

Oyun ve köprü ölçümleri yerel `olcumler/` klasörüne yazar. Bu klasör repoya ve dağıtım ZIP'ine girmez.

## Klasör yapısı

```text
FizyoOyun/
├── FizyoOyun.exe ve Unity derleme dosyaları
└── kopru/
    ├── hareket_yayini.py
    ├── kamera_test.py
    ├── models/pose_landmarker_full.task
    ├── requirements.txt
```

