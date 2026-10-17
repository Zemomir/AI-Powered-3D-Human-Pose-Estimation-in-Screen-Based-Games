# Fizyo-Oyun

Kamera kontrollü fizyoterapi oyunu, hemipleji tanılı çocuklar için geliştirilmiştir. TÜBİTAK 2209-B projesidir. Unity oyunu, MediaPipe kullanan Python kamera köprüsünden poz bilgisi alır.

## Nasıl oynanır?

- Gövdeni sağa veya sola eğerek motosikleti döndür.
- Öne eğilerek hızlan, geriye eğilerek yavaşla.
- Sarı-turuncu tişörtlü seyirci yaklaşınca, bulunduğu taraftaki elini omuz hizasının üstüne kaldırıp çak.
- Yol üzerindeki yıldızları topla.

## Laptopta kurulum

1. Python 3.13'ü [python.org](https://www.python.org/downloads/) üzerinden kur; kurulumda **Add Python to PATH** seçeneğini işaretle.
2. GitHub Releases'tan oyunun ZIP dosyasını indirip aç.
3. `FizyoOyun\kopru\kurulum.bat` dosyasını bir kez çalıştır. Paketleri ve poz modelini indirmek için internet gerekir.
4. `FizyoOyun\FizyoOyun.exe` dosyasını aç.

## Kamera ve konumlanma

Menüden **Laptop kamerası** veya **Telefon (DroidCam)** seç. Telefon seçeneğinde `IP:port` adresini gir. Kameradan 2-3 metre uzakta, ayakta dur; omuzların ve kalçaların görüntüde olsun. Masaya oturunca kalçalar görünmez ve direksiyon kontrolü çalışmaz.

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
    └── kurulum.bat
```

