@setlocal enableextensions
@cd /d "%~dp0"

powershell -ExecutionPolicy Bypass -F uninstall.ps1
