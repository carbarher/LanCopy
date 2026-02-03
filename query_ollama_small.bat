@echo off
echo Consultando a Ollama con modelo pequeño...
echo.

REM Intentar con diferentes modelos pequeños
type c:\p2p\ollama_prompt.txt | ollama run llama3.2:1b > c:\p2p\ollama_response.txt 2>&1

if not exist c:\p2p\ollama_response.txt (
    echo Intentando con llama2...
    type c:\p2p\ollama_prompt.txt | ollama run llama2 > c:\p2p\ollama_response.txt 2>&1
)

if exist c:\p2p\ollama_response.txt (
    echo.
    echo === RESPUESTA DE OLLAMA ===
    type c:\p2p\ollama_response.txt
    echo.
    echo === FIN RESPUESTA ===
) else (
    echo Error: No se pudo obtener respuesta
)
