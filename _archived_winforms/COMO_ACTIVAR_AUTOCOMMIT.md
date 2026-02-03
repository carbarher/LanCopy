# 🔄 Cómo Activar el Auto-Commit Permanente

## Opción 1: Instalación Automática (RECOMENDADO)

### Paso 1: Ejecutar como Administrador

1. Ve a la carpeta: `c:\p2p\SlskDown`
2. Busca el archivo: **`INSTALAR_AUTOCOMMIT_ADMIN.bat`**
3. **Haz clic derecho** sobre el archivo
4. Selecciona: **"Ejecutar como administrador"**
5. Confirma el UAC (Control de Cuentas de Usuario)

### Paso 2: Verificar Instalación

El script mostrará:
```
✓ INSTALACION EXITOSA
La tarea "SlskDown_AutoCommit" se ejecutará:
  - Al iniciar Windows
  - Hará commits automáticos cada hora
  - Se ejecutará en segundo plano
```

### Paso 3: ¡Listo!

El auto-commit ya está funcionando y hará commits cada hora automáticamente.

---

## Opción 2: Instalación Manual

Si prefieres hacerlo manualmente, ejecuta en CMD como Administrador:

```bash
cd c:\p2p\SlskDown
schtasks /create /tn "SlskDown_AutoCommit" /tr "c:\p2p\SlskDown\auto_commit_service.bat" /sc onstart /ru "%USERNAME%" /rl highest /f
schtasks /run /tn "SlskDown_AutoCommit"
```

---

## Opción 3: Ejecución Temporal (Sin Tarea Programada)

Si solo quieres auto-commit mientras trabajas (sin instalación permanente):

1. Abre una ventana de CMD
2. Ejecuta:
```bash
cd c:\p2p\SlskDown
auto_commit_service.bat
```
3. Deja la ventana abierta mientras trabajas
4. Hará commits cada hora mientras la ventana esté abierta

---

## 🔍 Verificar si Está Activo

Para comprobar si el auto-commit está funcionando:

```bash
schtasks /query /tn "SlskDown_AutoCommit"
```

**Si está activo verás:**
```
Folder: \
TaskName                                 Next Run Time          Status
======================================== ====================== ===============
SlskDown_AutoCommit                      N/A                    Running
```

**Si NO está instalado verás:**
```
ERROR: The system cannot find the file specified.
```

---

## 🛠️ Comandos Útiles

### Ver estado de la tarea
```bash
schtasks /query /tn "SlskDown_AutoCommit"
```

### Iniciar manualmente
```bash
schtasks /run /tn "SlskDown_AutoCommit"
```

### Detener temporalmente
```bash
schtasks /end /tn "SlskDown_AutoCommit"
```

### Eliminar la tarea
```bash
schtasks /delete /tn "SlskDown_AutoCommit" /f
```

### Ver historial de commits
```bash
git log --oneline -10
```

---

## 📋 Qué Hace el Auto-Commit

Cada hora automáticamente:
1. Ejecuta `git add -A` (agrega todos los cambios)
2. Ejecuta `git commit -m "Auto-save: YYYYMMDD_HHMMSS"` (hace commit con timestamp)
3. Espera 1 hora
4. Repite el proceso

**Ventajas:**
- ✅ Nunca más perderás trabajo
- ✅ Historial completo de cambios cada hora
- ✅ Puedes revertir a cualquier punto
- ✅ Funciona en segundo plano sin molestar

---

## ⚠️ Importante

- El auto-commit NO hace `git push` automáticamente (solo commits locales)
- Los commits se guardan en tu repositorio local
- Puedes hacer push manual cuando quieras: `git push`
- El auto-commit se ejecuta incluso si no hay cambios (commit vacío se ignora automáticamente)

---

## 🎯 Resumen Rápido

**Para activar ahora:**
1. Ejecuta `INSTALAR_AUTOCOMMIT_ADMIN.bat` como Administrador
2. Confirma que ves "INSTALACION EXITOSA"
3. ¡Listo! Ya está funcionando

**Para verificar:**
```bash
schtasks /query /tn "SlskDown_AutoCommit"
```

**Para hacer commit manual ahora:**
```bash
git add -A
git commit -m "Descripción de cambios"
```
