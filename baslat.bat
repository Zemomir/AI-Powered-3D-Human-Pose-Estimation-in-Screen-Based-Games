@echo off
rem Kamera secimi artik oyunun menusunde yapilir.
cd /d "%~dp0"
if not exist oyun_derleme\FizyoOyun.exe (
  echo Oyun derlenmemis: oyun_derleme\FizyoOyun.exe yok.
  pause
  exit /b 1
)
if not exist .venv\Scripts\python.exe (
  echo Python ortami yok. Once kurulum.bat dosyasini calistirin.
  pause
  exit /b 1
)
start "" oyun_derleme\FizyoOyun.exe
