# p2p

![.NET CI](https://github.com/carbarher/p2p/actions/workflows/dotnet.yml/badge.svg?branch=main)

Monorepo con integración Rust y la aplicación de escritorio **SlskDownAvalonia** (submódulo Git).

## Clonar el repositorio

Incluye el submódulo en un solo paso:

```bash
git clone --recurse-submodules https://github.com/carbarher/p2p.git
cd p2p
```

Si ya clonaste sin submódulos:

```bash
git submodule update --init --recursive
```

Para actualizar el puntero del submódulo a lo último en `origin` (después de un `git pull` en el repo padre):

```bash
git submodule update --remote SlskDownAvalonia
```

## Compilar la app Avalonia

Desde la raíz de `p2p`:

```bash
dotnet build SlskDownAvalonia/SlskDownAvalonia.csproj -c Release
dotnet test SlskDownAvalonia/SlskDownAvalonia.Tests.csproj -c Release
```

El workflow [`.github/workflows/dotnet.yml`](.github/workflows/dotnet.yml) ejecuta restore, build y tests en cada push/PR a `main` o `master`.

## Versionado y releases (SlskDownAvalonia)

Las versiones publicables de la aplicación se etiquetan en el **repositorio del submódulo**, no en el monorepo padre:

1. Abre [`SlskDownAvalonia/CHANGELOG.md`](SlskDownAvalonia/CHANGELOG.md) y mueve el contenido de **Unreleased** a una sección con el número de versión y la fecha, por ejemplo `## [1.2.3] - 2026-03-27`.
2. Commit y push en `SlskDownAvalonia`.
3. Crea y sube un tag semántico `v*` (p. ej. `v1.2.3`):

   ```bash
   cd SlskDownAvalonia
   git tag -a v1.2.3 -m "v1.2.3"
   git push origin v1.2.3
   ```

4. El workflow **Release** del repo [`SlskDownAvalonia`](https://github.com/carbarher/SlskDownAvalonia) crea automáticamente un [GitHub Release](https://docs.github.com/en/repositories/releasing-projects-on-github/about-releases) con notas generadas a partir de los commits respecto al tag anterior.

5. Opcional: en el repo **p2p**, actualiza el puntero del submódulo al commit etiquetado y haz commit para que el monorepo apunte a esa versión de la app.

Si solo quieres marcar un hito en el monorepo completo, puedes usar otro esquema de tags en `p2p` (p. ej. `p2p-2026.03.27`); no dispara el release de la app, que sigue el flujo anterior.
