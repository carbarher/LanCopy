with open(r'c:\p2p\SlskDown\MainForm.cs', 'r', encoding='utf-8') as f:
    lines = f.readlines()

brace_count = 0
class_closed_at = None

for i, line in enumerate(lines, 1):
    open_braces = line.count('{')
    close_braces = line.count('}')
    
    brace_count += open_braces - close_braces
    
    # Detectar cuando la clase MainForm se cierra (balance = 1, solo queda namespace)
    if brace_count == 1 and class_closed_at is None and i > 100:
        class_closed_at = i
        print(f"ALERTA: La clase MainForm se cierra en la linea {i}")
        print(f"  Contenido: {line.rstrip()}")
        print(f"\nSiguientes 10 lineas:")
        for j in range(10):
            if i + j < len(lines):
                print(f"  {i+j+1}: {lines[i+j].rstrip()}")
        break

if class_closed_at is None:
    print("No se detecto cierre prematuro de la clase")
    print(f"Balance final: {brace_count}")
