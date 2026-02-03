# Dividir archivo de autores en chunks de 500 líneas
import sys
import traceback

try:
    log_file = open('dividir_log.txt', 'w', encoding='utf-8')
    
    log_file.write("=== Iniciando división de archivo ===\n")
    print("Iniciando...")
    
    with open('autores_sf_2500.txt', 'r', encoding='utf-8') as f:
        lines = f.readlines()
    
    total_lines = len(lines)
    chunk_size = 500
    num_chunks = (total_lines + chunk_size - 1) // chunk_size
    
    log_file.write(f"Total líneas: {total_lines}\n")
    log_file.write(f"Dividiendo en {num_chunks} archivos\n\n")
    print(f"Total líneas: {total_lines}")
    
    for i in range(num_chunks):
        start = i * chunk_size
        end = min(start + chunk_size, total_lines)
        chunk_lines = lines[start:end]
        
        output_file = f'autores_sf_2500_{i+1}.txt'
        with open(output_file, 'w', encoding='utf-8') as f:
            f.writelines(chunk_lines)
        
        msg = f"✅ {output_file}: {len(chunk_lines)} líneas\n"
        log_file.write(msg)
        print(msg.strip())
    
    final_msg = f"\n✅ División completa: {num_chunks} archivos creados"
    log_file.write(final_msg)
    print(final_msg)
    
    log_file.close()
    
except Exception as e:
    error_msg = f"ERROR: {str(e)}\n{traceback.format_exc()}"
    print(error_msg)
    try:
        log_file.write(error_msg)
        log_file.close()
    except:
        pass
    sys.exit(1)
