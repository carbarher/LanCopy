@echo off
del c:\p2p\ollama_response.txt 2>nul
echo Consultando a Ollama con modelo 1B...
type c:\p2p\ollama_prompt.txt | ollama run llama3.2:1b > c:\p2p\ollama_response.txt 2>&1
echo Respuesta guardada
