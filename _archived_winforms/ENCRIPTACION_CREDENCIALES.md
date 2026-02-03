# 🔒 Encriptación de Credenciales - SlskDown

## 📋 Resumen

Se ha implementado **encriptación de credenciales** usando **DPAPI** (Data Protection API) de Windows para proteger tu usuario y contraseña de Soulseek.

---

## 🎯 ¿Qué es DPAPI?

**DPAPI** (Data Protection API) es una API de Windows que:
- ✅ Encripta datos usando claves del sistema operativo
- ✅ Solo el **mismo usuario** en la **misma máquina** puede desencriptar
- ✅ No requiere gestión manual de claves
- ✅ Es el estándar de Windows para almacenar credenciales

---

## 🔧 ¿Cómo Funciona?

### Al Guardar Configuración

Cuando haces clic en **"Guardar"** en la pestaña Config, aparece un diálogo:

```
¿Deseas guardar las credenciales de forma SEGURA (encriptadas)?

✅ SÍ: Las credenciales se encriptarán con DPAPI de Windows
   (Solo tú podrás desencriptarlas en esta máquina)

❌ NO: Se guardarán en texto plano (menos seguro)

Recomendado: SÍ
```

### Opción 1: Guardar Encriptado (Recomendado)

Si eliges **SÍ**:

1. **Usuario y contraseña** se encriptan con DPAPI
2. Se guarda en `config_secure.json`
3. Indicador en barra de estado: **🔒 Config guardada (ENCRIPTADA)**
4. Mensaje de confirmación:
   ```
   ✅ Configuración guardada de forma SEGURA.
   
   Tus credenciales están encriptadas con DPAPI.
   La aplicación se reconectará con las nuevas credenciales.
   ```

### Opción 2: Guardar Sin Encriptar

Si eliges **NO**:

1. **Usuario y contraseña** se guardan en texto plano
2. Se guarda en `config.json`
3. Indicador en barra de estado: **⚠️ Config guardada (TEXTO PLANO)**
4. Mensaje de advertencia:
   ```
   ⚠️ Configuración guardada en TEXTO PLANO.
   
   Tus credenciales NO están encriptadas.
   La aplicación se reconectará con las nuevas credenciales.
   ```

---

## 📂 Archivos Generados

### config_secure.json (Encriptado)
```json
{
  "DownloadDirectory": "c:\\p2p\\downloads",
  "SearchTimeout": 450,
  "ResponseLimit": 50,
  "FileLimit": 1000,
  "AutoConnect": true,
  "EncryptedUsername": [123, 45, 67, 89, ...],  // Bytes encriptados
  "EncryptedPassword": [234, 56, 78, 90, ...]   // Bytes encriptados
}
```

### config.json (Texto Plano)
```json
{
  "username": "carbar",
  "password": "Carlos66*",
  "downloadDir": "c:\\p2p\\downloads",
  "searchTimeoutSecs": 450,
  "responseLimit": 100,
  "fileLimit": 200
}
```

---

## 🔄 Carga Automática

Al iniciar la aplicación:

1. **Busca `config_secure.json`**
   - Si existe → Carga credenciales encriptadas
   - Log: `"Configuración encriptada cargada exitosamente"`

2. **Si no existe, busca `config.json`**
   - Carga credenciales en texto plano
   - Log: `"Configuración en texto plano cargada (considera usar encriptación)"`

---

## 🛡️ Seguridad

### ✅ Ventajas de Encriptar

- **Protección**: Nadie puede leer tus credenciales del archivo
- **Automático**: Windows gestiona las claves
- **Transparente**: La app desencripta automáticamente
- **Estándar**: Usado por Chrome, Edge, etc.

### ⚠️ Limitaciones

- **Mismo usuario**: Solo funciona con tu cuenta de Windows
- **Misma máquina**: No puedes copiar el archivo a otra PC
- **Backup**: Si formateas, pierdes acceso (guarda backup del `config.json` original)

---

## 💻 Implementación Técnica

### Servicios Utilizados

```csharp
// SecurityService - Encriptación DPAPI
public byte[] Protect(string data)
{
    byte[] dataBytes = Encoding.UTF8.GetBytes(data);
    return ProtectedData.Protect(
        dataBytes,
        null,
        DataProtectionScope.CurrentUser
    );
}

public string Unprotect(byte[] encryptedData)
{
    byte[] decryptedBytes = ProtectedData.Unprotect(
        encryptedData,
        null,
        DataProtectionScope.CurrentUser
    );
    return Encoding.UTF8.GetString(decryptedBytes);
}
```

### ConfigService - Gestión de Configuración

```csharp
// Guardar credenciales encriptadas
public void SaveCredentials(string username, string password)
{
    var config = LoadConfig();
    config.EncryptedUsername = _securityService.Protect(username);
    config.EncryptedPassword = _securityService.Protect(password);
    SaveConfig(config);
}

// Obtener credenciales desencriptadas
public (string username, string password) GetCredentials()
{
    var config = LoadConfig();
    string username = _securityService.Unprotect(config.EncryptedUsername);
    string password = _securityService.Unprotect(config.EncryptedPassword);
    return (username, password);
}
```

---

## 🔄 Migración

### De Texto Plano a Encriptado

1. Abre SlskDown
2. Ve a pestaña **⚙️ Config**
3. Verifica que usuario y contraseña son correctos
4. Haz clic en **"Guardar"**
5. Elige **"SÍ"** cuando pregunte por encriptación
6. ✅ Listo - Ahora usa `config_secure.json`

### De Encriptado a Texto Plano

1. Elimina `config_secure.json`
2. Abre SlskDown
3. Ve a pestaña **⚙️ Config**
4. Ingresa credenciales
5. Haz clic en **"Guardar"**
6. Elige **"NO"** cuando pregunte por encriptación
7. ✅ Listo - Ahora usa `config.json`

---

## 📊 Comparación

| Característica | Encriptado | Texto Plano |
|---------------|-----------|-------------|
| **Archivo** | `config_secure.json` | `config.json` |
| **Seguridad** | 🔒 Alta | ⚠️ Baja |
| **Legibilidad** | ❌ No legible | ✅ Legible |
| **Portabilidad** | ❌ Solo esta PC | ✅ Cualquier PC |
| **Backup** | ⚠️ Requiere cuidado | ✅ Fácil |
| **Recomendado** | ✅ **SÍ** | ❌ No |

---

## 🎯 Recomendaciones

### ✅ Usa Encriptación Si:
- Compartes la PC con otras personas
- Guardas la carpeta en la nube (Dropbox, OneDrive)
- Quieres máxima seguridad
- No necesitas mover la config a otra PC

### ⚠️ Usa Texto Plano Si:
- Eres el único usuario de la PC
- Necesitas hacer backup fácilmente
- Vas a mover la config a otra PC
- Prefieres simplicidad sobre seguridad

---

## 🐛 Solución de Problemas

### Error: "No se puede desencriptar"

**Causa**: Intentas usar `config_secure.json` de otra PC o usuario

**Solución**:
1. Elimina `config_secure.json`
2. Reinicia la app
3. Configura credenciales de nuevo

### Error: "ConfigService no disponible"

**Causa**: Servicios no inicializados

**Solución**: La app usa fallback automático a `config.json`

---

## 📝 Logs

Los eventos de encriptación se registran en `logs/slskdown-YYYY-MM-DD.txt`:

```
[2025-10-30 17:00:00.123] [INFO] Configuración encriptada cargada exitosamente
[2025-10-30 17:05:00.456] [INFO] Configuración guardada de forma segura para usuario: carbar
[2025-10-30 17:10:00.789] [WARN] ConfigService no disponible, usando LoadConfig
```

---

## 🎉 Conclusión

La encriptación de credenciales está **completamente implementada y funcional**. 

**Recomendación**: Usa siempre la opción encriptada (SÍ) para máxima seguridad.

---

**Fecha**: 30 de octubre de 2025  
**Versión**: SlskDown 1.2  
**Estado**: ✅ Implementado y probado
