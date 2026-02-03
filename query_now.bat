@echo off
del c:\p2p\ollama_response.txt 2>nul
echo Consultando a Ollama (modelo ya descargado)...
echo.
type c:\p2p\ollama_prompt.txt | ollama run llama3.2:1b --verbose > c:\p2p\ollama_response.txt 2>&1
if %ERRORLEVEL% EQU 0 (
    echo.
    echo === RESPUESTA ===
    type c:\p2p\ollama_response.txt
) else (
    echo Error al consultar
    type c:\p2p\ollama_response.txt
)
