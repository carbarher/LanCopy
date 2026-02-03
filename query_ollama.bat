@echo off
echo Consultando a Ollama...
echo.

type c:\p2p\ollama_prompt.txt | ollama run llama3.2 > c:\p2p\ollama_response.txt 2>&1

if exist c:\p2p\ollama_response.txt (
    echo Respuesta guardada en c:\p2p\ollama_response.txt
    type c:\p2p\ollama_response.txt
) else (
    echo Error: No se pudo obtener respuesta
)
