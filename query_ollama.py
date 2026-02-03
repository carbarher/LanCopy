import requests
import json
import sys

def query_ollama(prompt, model="llama3.2"):
    url = "http://localhost:11434/api/generate"
    
    payload = {
        "model": model,
        "prompt": prompt,
        "stream": False
    }
    
    try:
        print(f"Consultando a Ollama con modelo {model}...")
        print("Esto puede tardar 1-2 minutos...\n")
        
        response = requests.post(url, json=payload, timeout=180)
        
        if response.status_code == 200:
            result = response.json()
            return result.get("response", "No response")
        else:
            return f"Error: Status {response.status_code}"
            
    except requests.exceptions.ConnectionError:
        return "Error: No se puede conectar a Ollama. ¿Está corriendo?"
    except requests.exceptions.Timeout:
        return "Error: Timeout - Ollama tardó demasiado en responder"
    except Exception as e:
        return f"Error: {str(e)}"

def list_models():
    try:
        response = requests.get("http://localhost:11434/api/tags", timeout=5)
        if response.status_code == 200:
            data = response.json()
            return [model["name"] for model in data.get("models", [])]
        return []
    except:
        return []

if __name__ == "__main__":
    # Leer el prompt
    with open("c:\\p2p\\ollama_prompt.txt", "r", encoding="utf-8") as f:
        prompt = f.read()
    
    # Listar modelos disponibles
    models = list_models()
    print(f"Modelos disponibles: {models}\n")
    
    # Usar el primer modelo disponible o el especificado
    model = models[0] if models else "llama3.2"
    
    # Consultar
    response = query_ollama(prompt, model)
    
    # Guardar respuesta
    with open("c:\\p2p\\ollama_response.txt", "w", encoding="utf-8") as f:
        f.write(f"=== RESPUESTA DE OLLAMA (modelo: {model}) ===\n\n")
        f.write(response)
        f.write("\n\n=== FIN RESPUESTA ===")
    
    print("\n" + "="*60)
    print(response)
    print("="*60)
    print(f"\nRespuesta guardada en: c:\\p2p\\ollama_response.txt")
