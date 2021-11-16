@setlocal enableextensions
@cd /d "%~dp0"

powershell -ExecutionPolicy Bypass -F uninstall.ps1
powershell -ExecutionPolicy Bypass -F install.ps1
pause
