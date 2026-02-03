# Opciones de VPN con CLI para Windows

## 🎯 Objetivo
Cambiar IP automáticamente desde SlskDown cuando se detecta bloqueo del servidor Soulseek.

---

## ✅ Opciones Viables con CLI Real

### 1. **Mullvad VPN** (Recomendado)
- **Precio**: €5/mes (~$5.50 USD)
- **CLI**: ✅ Completo y robusto
- **Windows**: ✅ Soportado
- **Comandos básicos**:
  ```bash
  # Conectar
  mullvad connect
  
  # Cambiar país
  mullvad relay set location us
  mullvad relay set location es
  mullvad relay set location fr
  
  # Desconectar
  mullvad disconnect
  
  # Estado
  mullvad status -v
  ```

**Ventajas**:
- CLI oficial y bien documentado
- Cambio de servidor muy rápido (~2 segundos)
- WireGuard (más rápido que OpenVPN)
- Sin logs, privacidad máxima
- Acepta efectivo/crypto (anónimo)

**Desventajas**:
- No es gratis (€5/mes)

**Instalación**:
1. Descargar: https://mullvad.net/download/windows
2. Instalar
3. Crear cuenta: https://mullvad.net/account/create
4. Login: `mullvad account login <account-number>`

---

### 2. **Windscribe VPN**
- **Precio**: Gratis (10GB/mes) o $9/mes (ilimitado)
- **CLI**: ✅ Disponible
- **Windows**: ✅ Soportado
- **Comandos básicos**:
  ```bash
  # Conectar
  windscribe connect
  
  # Cambiar ubicación
  windscribe connect US
  windscribe connect ES
  windscribe connect FR
  
  # Desconectar
  windscribe disconnect
  
  # Estado
  windscribe status
  
  # Listar ubicaciones
  windscribe locations
  ```

**Ventajas**:
- Plan gratuito (10GB/mes)
- CLI funcional
- Muchas ubicaciones

**Desventajas**:
- Límite de 10GB en plan gratuito
- CLI menos documentado que Mullvad

**Instalación**:
1. Descargar: https://windscribe.com/download
2. Instalar
3. Crear cuenta gratuita
4. Login desde GUI o CLI

---

### 3. **OpenVPN Connect**
- **Precio**: Gratis (requiere servidor propio o proveedor)
- **CLI**: ✅ Disponible
- **Windows**: ✅ Soportado
- **Comandos básicos**:
  ```bash
  # Importar perfil
  OpenVPNConnect.exe --import-profile="C:\path\to\config.ovpn"
  
  # Conectar
  OpenVPNConnect.exe --connect=<profile-id>
  
  # Desconectar
  OpenVPNConnect.exe --disconnect=<profile-id>
  ```

**Ventajas**:
- Gratis si tienes servidor propio
- Estándar de la industria

**Desventajas**:
- Requiere configurar servidor propio o pagar proveedor
- Más lento que WireGuard
- Más complejo de configurar

---

## ❌ Opciones NO Viables

### ProtonVPN
- ❌ No tiene CLI oficial en Windows
- ❌ Solo GUI con hotkeys no confiables
- ❌ No se puede automatizar

### NordVPN
- ❌ CLI solo en Linux/Mac
- ❌ Windows solo GUI

### ExpressVPN
- ❌ No tiene CLI público

---

## 🏆 Recomendación Final

### Para Producción: **Mullvad VPN**
```csharp
// Ejemplo de integración en VPNManager.cs
public async Task<bool> ConnectAsync()
{
    try
    {
        // Cambiar a país aleatorio
        var countries = new[] { "us", "es", "fr", "de", "nl", "se" };
        var country = countries[Random.Shared.Next(countries.Length)];
        
        await RunCommand("mullvad", $"relay set location {country}");
        await Task.Delay(1000);
        
        var result = await RunCommand("mullvad", "connect");
        
        if (result.Contains("Connected"))
        {
            Log("✅ Mullvad VPN conectado");
            return true;
        }
        
        return false;
    }
    catch (Exception ex)
    {
        Log($"❌ Error conectando Mullvad: {ex.Message}");
        return false;
    }
}
```

**Costo**: €5/mes (~€60/año)
**Beneficio**: Cambio de IP automático en 2-3 segundos

### Para Pruebas: **Windscribe VPN (Gratis)**
- 10GB/mes gratis
- Suficiente para pruebas y uso ocasional
- Mismo código, solo cambiar comandos:
  ```csharp
  await RunCommand("windscribe", "connect US");
  ```

---

## 📝 Próximos Pasos

1. **Decidir VPN**: Mullvad (pago) o Windscribe (gratis)
2. **Instalar y configurar**
3. **Modificar VPNManager.cs** para usar CLI real
4. **Probar cambio automático de IP**

---

## 💡 Alternativa Sin VPN

Si no quieres pagar VPN, el sistema actual con **delays de 60s** funciona:
- ✅ Conecta después de esperar
- ✅ Sin costo adicional
- ❌ No cambia IP (pero no es necesario si el delay funciona)

El bloqueo de Soulseek parece ser **temporal (~60s)**, no permanente por IP.
