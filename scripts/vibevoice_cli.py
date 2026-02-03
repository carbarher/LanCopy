#!/usr/bin/env python3
"""VibeVoice CLI sampler.

Genera clips (incluye español) desde terminal usando forks comunitarios de
VibeVoice alojados en Hugging Face. Ideal para escuchar el timbre sin abrir
la app principal.

Instalación previa::

    pip install --upgrade "torch>=2.1"
    pip install transformers soundfile accelerate sentencepiece

Ejemplos rápidos::

    python scripts/vibevoice_cli.py --text "Hola" --output-dir outputs

    python scripts/vibevoice_cli.py \
        --text "Fragmento uno." \
        --text "Fragmento dos." \
        --model-id shijincai/VibeVoice-7B

    python scripts/vibevoice_cli.py \
        --text-file frases.txt \
        --device cuda \
        --dtype float16

    python scripts/vibevoice_cli.py \
        --text "Hola" \
        --extra-args '{"voice": "spanish_female"}'

La mayoría de forks necesitan ``trust_remote_code=True`` para registrar su
pipeline personalizada.
"""

from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import List, Optional

import soundfile as sf  # type: ignore
import torch
from transformers import pipeline


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Generar muestras con VibeVoice"
    )
    parser.add_argument(
        "--model-id",
        default="shijincai/VibeVoice-1.5B",
        help=(
            "Repositorio Hugging Face a usar "
            "(por defecto: shijincai/VibeVoice-1.5B)"
        ),
    )
    parser.add_argument(
        "--text",
        action="append",
        default=None,
        help=(
            "Texto a sintetizar (repetible). Si se usa --text-file, "
            "ambos se combinan."
        ),
    )
    parser.add_argument(
        "--text-file",
        type=Path,
        help=(
            "Ruta a un archivo UTF-8 con una frase por línea (líneas vacías "
            "se ignoran)."
        ),
    )
    parser.add_argument(
        "--output-dir",
        type=Path,
        default=Path("outputs/vibevoice"),
        help=(
            "Directorio destino para los WAV generados (se crea "
            "automáticamente)."
        ),
    )
    parser.add_argument(
        "--prefix",
        default="sample",
        help="Prefijo para los archivos WAV (por defecto: sample).",
    )
    parser.add_argument(
        "--device",
        default="auto",
        help=(
            "cpu | cuda | cuda:0 ... (auto = usa CUDA si está disponible)."
        ),
    )
    parser.add_argument(
        "--dtype",
        default="auto",
        choices=("auto", "float32", "float16", "bfloat16"),
        help=(
            "Precisión numérica para el modelo (auto detecta en función del "
            "dispositivo)."
        ),
    )
    parser.add_argument(
        "--extra-args",
        default=None,
        help=(
            "JSON opcional con parámetros adicionales a la pipeline "
            "(p. ej. {'voice': 'spanish_female', 'temperature': 0.7}). "
            "La estructura depende del fork/modelo que uses."
        ),
    )
    parser.add_argument(
        "--sample-rate",
        type=int,
        default=None,
        help=(
            "Forzar sample rate al guardar (si no se indica, se usa el "
            "devuelto por el modelo)."
        ),
    )
    return parser.parse_args()


def resolve_device(device_arg: str) -> str:
    if device_arg == "auto":
        return "cuda" if torch.cuda.is_available() else "cpu"
    if device_arg.startswith("cuda") and not torch.cuda.is_available():
        raise RuntimeError(
            "Se solicitó CUDA pero no hay GPU disponible. Usa --device cpu"
        )
    return device_arg


def resolve_dtype(dtype_arg: str, device: str) -> Optional[torch.dtype]:
    if dtype_arg == "auto":
        return torch.float16 if device.startswith("cuda") else torch.float32
    mapping = {
        "float32": torch.float32,
        "float16": torch.float16,
        "bfloat16": torch.bfloat16,
    }
    return mapping[dtype_arg]


def load_texts(text_args: Optional[List[str]], text_file: Optional[Path]) -> List[str]:
    texts: List[str] = []
    if text_args:
        texts.extend(
            t.strip()
            for t in text_args
            if t.strip()
        )
    if text_file:
        if not text_file.exists():
            raise FileNotFoundError(
                f"No se encontró el archivo de textos: {text_file}"
            )
        lines = text_file.read_text(encoding="utf-8").splitlines()
        texts.extend(line.strip() for line in lines if line.strip())
    texts = [t for t in texts if t]
    if not texts:
        raise ValueError(
            "No hay textos para sintetizar. Usa --text o --text-file."
        )
    return texts


def ensure_output_dir(path: Path) -> None:
    path.mkdir(parents=True, exist_ok=True)


def build_pipeline(model_id: str, device: str, torch_dtype: Optional[torch.dtype]):
    pipe = pipeline(
        task="text-to-speech",
        model=model_id,
        device=device,
        torch_dtype=torch_dtype,
        trust_remote_code=True,
    )
    return pipe


def parse_extra_args(extra_args: Optional[str]) -> dict:
    if not extra_args:
        return {}
    try:
        data = json.loads(extra_args)
    except json.JSONDecodeError as exc:
        raise ValueError(
            "No se pudo parsear --extra-args como JSON válido: "
            f"{exc}"
        ) from exc
    if not isinstance(data, dict):
        raise ValueError(
            "--extra-args debe ser un objeto JSON (p. ej. {\"voice\": "
            "\"spanish_female\"})"
        )
    return data


def save_audio(array, sample_rate: int, target_path: Path) -> None:
    target_path.parent.mkdir(parents=True, exist_ok=True)
    sf.write(target_path, array, sample_rate)


def main() -> None:
    args = parse_args()
    device = resolve_device(args.device)
    torch_dtype = resolve_dtype(args.dtype, device)
    texts = load_texts(args.text, args.text_file)
    ensure_output_dir(args.output_dir)
    extra_kwargs = parse_extra_args(args.extra_args)

    print(
        f"[+] Cargando modelo {args.model_id} en {device} "
        f"(dtype={torch_dtype}) ..."
    )
    pipe = build_pipeline(args.model_id, device, torch_dtype)
    print("[+] Modelo listo. Generando muestras...")

    for idx, text in enumerate(texts, start=1):
        portada = text[:60] + ("…" if len(text) > 60 else "")
        print(f"    • [{idx}/{len(texts)}] \"{portada}\"")
        result = pipe(text, **extra_kwargs)

        if isinstance(result, dict):
            audio = result.get("audio") or result.get("speech")
            sr = result.get("sampling_rate") or result.get("sample_rate")
        else:
            raise RuntimeError(
                "El resultado devuelto por la pipeline no es un dict. "
                "Actualiza transformers o revisa el fork del modelo."
            )

        if audio is None or sr is None:
            raise RuntimeError(
                "No se encontraron claves 'audio'/'speech' y 'sampling_rate' en "
                "la salida. Revisa la API del modelo o usa --extra-args para "
                "ajustarla."
            )

        target_sr = args.sample_rate or sr
        wav_path = args.output_dir / f"{args.prefix}_{idx:02d}.wav"
        save_audio(audio, target_sr, wav_path)
        print(f"      ↳ Guardado en {wav_path.as_posix()}")

    print(
        "[✓] Listo. Reproduce los WAV generados para evaluar la calidad de las "
        "voces."
    )


if __name__ == "__main__":
    main()
