@echo off
setlocal

set "ROOT=%~dp0"
set "CARGO=%USERPROFILE%\.cargo\bin\cargo.exe"
set "MANIFEST=%ROOT%gomoku_rs\Cargo.toml"

if not exist "%MANIFEST%" (
    echo No se encontro %MANIFEST%
    exit /b 1
)

if exist "%CARGO%" (
    "%CARGO%" run --manifest-path "%MANIFEST%"
) else (
    cargo run --manifest-path "%MANIFEST%"
)

exit /b %ERRORLEVEL%