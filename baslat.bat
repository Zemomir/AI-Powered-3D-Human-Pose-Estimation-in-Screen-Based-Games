@echo off
rem Fizyo-Oyun: kamerayi (telefon) ve oyunu birlikte acar.
rem Telefonun IP'si degisirse sadece asagidaki satiri duzelt (DroidCam ekraninda yazar).
set KAMERA=http://192.168.1.144:4747/video

cd /d "%~dp0"
if not exist oyun_derleme\FizyoOyun.exe (
  echo Oyun derlenmemis: oyun_derleme\FizyoOyun.exe yok.
  pause
  exit /b 1
)
if not exist .venv\Scripts\python.exe (
  echo Python ortami yok: .venv\Scripts\python.exe bulunamadi.
  pause
  exit /b 1
)
rem cmd /k: kamera betigi hata verip kapanirsa mesaj ekranda kalsin
start "Fizyo-Oyun kamera" cmd /k .venv\Scripts\python.exe hareket_yayini.py %KAMERA%
start "" oyun_derleme\FizyoOyun.exe
