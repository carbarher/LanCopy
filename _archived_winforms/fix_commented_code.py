import re
import sys

def fix_commented_code(file_path):
    """
    Arregla código comentado incorrectamente en archivos C#.
    Busca patrones como:
    // ERROR: codigo(
        parametro1,
        parametro2
    );
    
    Y los convierte en:
    // ERROR: codigo(
    //    parametro1,
    //    parametro2
    // );
    """
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            lines = f.readlines()
    except:
        print(f"Error leyendo {file_path}")
        return False
    
    modified = False
    i = 0
    while i < len(lines):
        line = lines[i]
        
        # Buscar líneas que empiezan con // ERROR:
        if line.strip().startswith('// ERROR:'):
            # Verificar si la siguiente línea NO está comentada pero debería estarlo
            # (es parte del código comentado)
            if i + 1 < len(lines):
                next_line = lines[i + 1]
                # Si la siguiente línea tiene indentación y no está comentada
                if next_line.strip() and not next_line.strip().startswith('//'):
                    # Contar cuántas líneas consecutivas necesitan ser comentadas
                    j = i + 1
                    indent = len(next_line) - len(next_line.lstrip())
                    
                    # Buscar hasta encontrar el final del bloque
                    while j < len(lines):
                        current = lines[j]
                        if not current.strip():
                            j += 1
                            continue
                        
                        # Si ya está comentada, salir
                        if current.strip().startswith('//'):
                            break
                        
                        # Si la indentación disminuye significativamente, salir
                        current_indent = len(current) - len(current.lstrip())
                        if current_indent < indent - 4:
                            break
                        
                        # Comentar esta línea
                        lines[j] = current[:current_indent] + '// ' + current[current_indent:]
                        modified = True
                        j += 1
                        
                        # Si encontramos un ; o } al final, terminar
                        if current.rstrip().endswith((';', '}')):
                            break
        
        i += 1
    
    if modified:
        try:
            with open(file_path, 'w', encoding='utf-8') as f:
                f.writelines(lines)
            print(f"✅ Arreglado: {file_path}")
            return True
        except:
            print(f"❌ Error escribiendo {file_path}")
            return False
    else:
        print(f"⏭️  Sin cambios: {file_path}")
        return False

if __name__ == '__main__':
    import os
    
    files_to_fix = [
        r'Core\SearchManager.cs',
        r'Database\SlskDatabase.cs',
        r'MainForm.cs'
    ]
    
    base_path = r'c:\p2p\SlskDown'
    total_fixed = 0
    
    for file in files_to_fix:
        full_path = os.path.join(base_path, file)
        if os.path.exists(full_path):
            if fix_commented_code(full_path):
                total_fixed += 1
        else:
            print(f"❌ No existe: {full_path}")
    
    print(f"\n📊 Total archivos arreglados: {total_fixed}")
