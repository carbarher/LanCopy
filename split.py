with open('autores_sf_2500.txt', 'r', encoding='utf-8') as f:
    lines = [line for line in f]

# Chunk 1: lineas 1-500
with open('autores_sf_2500_1.txt', 'w', encoding='utf-8') as f:
    f.writelines(lines[0:500])

# Chunk 2: lineas 501-1000  
with open('autores_sf_2500_2.txt', 'w', encoding='utf-8') as f:
    f.writelines(lines[500:1000])

# Chunk 3: lineas 1001-1500
with open('autores_sf_2500_3.txt', 'w', encoding='utf-8') as f:
    f.writelines(lines[1000:1500])

# Chunk 4: lineas 1501-fin
with open('autores_sf_2500_4.txt', 'w', encoding='utf-8') as f:
    f.writelines(lines[1500:])

print('OK')
