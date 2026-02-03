# 🚀 Ejecutar Script para Ver HTML de aMule

## 📝 Instrucciones Rápidas:

### 1. Abre PowerShell
```powershell
cd c:\p2p
```

### 2. Edita la Contraseña (si no es "amule")
```powershell
notepad test_amule_simple.ps1
# Cambia la línea: $password = "amule"
# Por tu contraseña real de aMule
```

### 3. Ejecuta el Script
```powershell
.\test_amule_simple.ps1
```

### 4. Copia el Output Completo
- Copia TODO el texto que aparece entre:
  ```
  ========== HTML COMPLETO ==========
  [... HTML aquí ...]
  ========== FIN HTML ==========
  ```

### 5. Pégalo Aquí
- Pega el HTML completo en el chat

## 🎯 Qué Vamos a Ver:

El HTML nos dirá exactamente qué está pasando:

### ✅ Si es una página de resultados:
```html
<table>
  <tr><td>archivo.mp3</td><td>5 MB</td>...</tr>
</table>
```
→ Ajustaremos el parseo

### ⚠️ Si es una página de login:
```html
<form action="/login">
  <input type="password">
</form>
```
→ Implementaremos login HTTP

### ❌ Si es un error:
```html
<div>Invalid password</div>
```
→ Verificaremos la contraseña

## 📁 Archivo Guardado:

El script también guardará el HTML en:
```
c:\p2p\amule_response.html
```

Puedes abrirlo con un navegador o editor de texto.

---

**¡Ejecuta el script y pégame el HTML completo!** 🔍
