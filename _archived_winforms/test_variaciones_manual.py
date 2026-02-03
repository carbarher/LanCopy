import re
import unicodedata

def remove_accents(text):
    nfd = unicodedata.normalize('NFD', text)
    return ''.join(char for char in nfd if unicodedata.category(char) != 'Mn')

def generate_variations(author):
    variations = set()
    
    # 1. Original
    variations.add(author)
    
    # 2-4. Variantes con puntos
    if '.' in author:
        # 2. Sin puntos
        without_dots = author.replace('.', '')
        if without_dots.strip():
            variations.add(without_dots)
            
            # 3. Sin puntos, espacios normalizados
            normalized = re.sub(r'\s+', ' ', without_dots).strip()
            if normalized:
                variations.add(normalized)
        
        # 4. Con puntos, sin espacios entre iniciales
        with_dots_no_spaces = re.sub(r'\.(\s+)(?=[A-Z]\.)', '.', author)
        if with_dots_no_spaces != author and with_dots_no_spaces.strip():
            variations.add(with_dots_no_spaces)
    
    # 5. Sin tildes
    without_accents = remove_accents(author)
    if without_accents != author:
        variations.add(without_accents)
    
    # 6-7. Variantes sin puntos ni tildes
    if '.' in author:
        without_dots_or_accents = remove_accents(author.replace('.', ''))
        if without_dots_or_accents.strip():
            variations.add(without_dots_or_accents)
            
            # 7. Completamente normalizado
            fully_normalized = re.sub(r'\s+', ' ', without_dots_or_accents).strip()
            if fully_normalized:
                variations.add(fully_normalized)
    
    return variations

# Test con A. A. Pepito
author = "A. A. Pepito"
variations = generate_variations(author)

print(f"Autor: {author}")
print(f"Total variaciones: {len(variations)}")
print()
print("Variaciones generadas:")
for i, var in enumerate(sorted(variations), 1):
    print(f"{i}. {var}")

# Verificar si faltan las 2 variaciones mencionadas
expected_variations = {
    "A. A. Pepito",      # Original
    "A A Pepito",        # Sin puntos
    "AA Pepito",         # Sin puntos, espacios normalizados
    "A.A. Pepito",       # Con puntos, sin espacios
    "A. A. Pepito",      # Sin tildes (igual al original en este caso)
    "A A Pepito",        # Sin puntos ni tildes
    "AA Pepito"          # Completamente normalizado
}

print()
print("Análisis de variaciones esperadas:")
missing = set()
for expected in expected_variations:
    if expected in variations:
        print(f"✅ {expected}")
    else:
        print(f"❌ {expected} (FALTA)")
        missing.add(expected)

if missing:
    print(f"\n⚠️ Faltan {len(missing)} variaciones:")
    for var in missing:
        print(f"   - {var}")
else:
    print("\n✅ Todas las variaciones esperadas están presentes")
