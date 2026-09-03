@echo off
set message=%~1
if "%message%"=="" set message=Update code %date% %time%

echo === 1. Them tat ca file thay doi ===
git add .

echo === 2. Tao commit: "%message%" ===
git commit -m "%message%"

echo === 3. Day code len GitHub (origin main) ===
git push origin main

echo === Hoan tat day code len GitHub! ===
