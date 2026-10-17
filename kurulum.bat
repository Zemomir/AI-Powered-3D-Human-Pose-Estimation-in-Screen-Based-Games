@echo off
chcp 65001 >nul
cd /d "%~dp0"
set "PYTHON="
set "PYARGS="
py -3.13 --version >nul 2>&1
if not errorlevel 1 (set "PYTHON=py"& set "PYARGS=-3.13"& goto python_found)
py -3.12 --version >nul 2>&1
if not errorlevel 1 (set "PYTHON=py"& set "PYARGS=-3.12"& goto python_found)
py -3.11 --version >nul 2>&1
if not errorlevel 1 (set "PYTHON=py"& set "PYARGS=-3.11"& goto python_found)
py -3.10 --version >nul 2>&1
if not errorlevel 1 (set "PYTHON=py"& set "PYARGS=-3.10"& goto python_found)
python --version >nul 2>&1
if not errorlevel 1 (set "PYTHON=python"& set "PYARGS="& goto python_found)
echo Python 3.10-3.13 bulunamadi.
echo Python'u https://www.python.org/downloads/ adresinden kurun ve kurulumda "Add Python to PATH" secenegini isaretleyin.
pause
exit /b 1
:python_found
for /f "tokens=2" %%V in ('%PYTHON% %PYARGS% --version 2^>^&1') do set "PYVER=%%V"
echo Bulunan Python: %PYVER%
%PYTHON% %PYARGS% -c "import sys; raise SystemExit(0 if (3,10) <= sys.version_info[:2] <= (3,13) else 1)"
if errorlevel 1 goto bad_python
if not exist ".venv\Scripts\python.exe" (
  %PYTHON% %PYARGS% -m venv .venv
  if errorlevel 1 goto venv_error
)
.venv\Scripts\python.exe -m pip install --upgrade pip
if errorlevel 1 goto pip_error
.venv\Scripts\python.exe -m pip install -r requirements.txt
if errorlevel 1 goto deps_error
if not exist "models\pose_landmarker_full.task" (
  if not exist "models" mkdir "models"
  powershell -NoProfile -ExecutionPolicy Bypass -Command "Invoke-WebRequest -Uri 'https://storage.googleapis.com/mediapipe-models/pose_landmarker/pose_landmarker_full/float16/latest/pose_landmarker_full.task' -OutFile 'models\pose_landmarker_full.task'"
  if errorlevel 1 goto model_error
)
.venv\Scripts\python.exe -c "import cv2, mediapipe; print('tamam', cv2.__version__, mediapipe.__version__)"
if errorlevel 1 goto verify_error
echo Kurulum tamam. Oyunu FizyoOyun.exe ile acabilirsin.
pause
exit /b 0
:bad_python
echo Desteklenmeyen Python surumu. Python 3.10-3.13 kurun: https://www.python.org/downloads/ adresinden indirip "Add Python to PATH" secenegini isaretleyin.
pause
exit /b 1
:venv_error
echo Sanal Python ortami olusturulamadi.
pause
exit /b 1
:pip_error
echo pip guncellenemedi. Internet baglantinizi kontrol edin.
pause
exit /b 1
:deps_error
echo Python paketleri kurulamadi. Internet baglantinizi kontrol edin.
pause
exit /b 1
:model_error
echo Poz modeli indirilemedi. Internet baglantinizi kontrol edip kurulumu yeniden calistirin.
pause
exit /b 1
:verify_error
echo Kurulum dogrulanamadi. Hata mesajini kontrol edin.
pause
exit /b 1

