# 🔌 Configurar Auto-Conexión en SlskDown

## Pasos:

1. **Abrir SlskDown**
   - Ejecuta: `c:\p2p\slsk.bat`

2. **Ir a pestaña ⚙️ Configuración**

3. **Ingresar credenciales de Soulseek:**
   - Usuario: [tu_usuario]
   - Contraseña: [tu_contraseña]

4. **Activar checkbox "Auto-conectar al iniciar"**

5. **Hacer clic en 💾 Guardar**

## Seguridad:

- ✅ Las credenciales se guardan **ENCRIPTADAS** con DPAPI de Windows
- ✅ Solo funcionan en tu usuario y máquina
- ✅ Archivo: `config_secure.json` (encriptado)

## Beneficios:

- ✅ No necesitas conectar manualmente cada vez
- ✅ La app se conecta automáticamente al iniciar
- ✅ Puedes empezar a buscar inmediatamente

## Backup de credenciales:

```batch
@echo off
echo Haciendo backup de configuración...
copy /Y c:\p2p\SlskDown\config_secure.json c:\p2p\backups\config_secure_%date:~-4,4%%date:~-10,2%%date:~-7,2%.json
echo ✅ Backup creado!
pause
```

Guarda como: `c:\p2p\BACKUP_CONFIG.bat`

## Restaurar credenciales:

Si cambias de PC o reinstalar Windows, las credenciales encriptadas NO funcionarán.
Deberás volver a ingresarlas manualmente.

## Credenciales de prueba (del documento de estado):

Si no tienes cuenta, puedes usar estas credenciales de prueba:
- Usuario: `carbar`
- Contraseña: `Carlos66*`

**Nota:** Estas son credenciales de prueba del documento del proyecto.
Se recomienda crear tu propia cuenta en https://www.slsknet.org/
