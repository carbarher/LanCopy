# Test directo de variaciones
author = "A. A. Pepito"
print(f"Autor: {author}")

# Variaciones actuales según el código
variations = set()

# 1. Original
variations.add(author)
print(f"1. Original: {author}")

# 2. Sin puntos
without_dots = author.replace('.', '')
variations.add(without_dots)
print(f"2. Sin puntos: {without_dots}")

# 3. Sin puntos, espacios normalizados
import re
normalized = re.sub(r'\s+', ' ', without_dots).strip()
variations.add(normalized)
print(f"3. Normalizado: {normalized}")

# 4. Con puntos, sin espacios entre iniciales
with_dots_no_spaces = re.sub(r'\.(\s+)(?=[A-Z]\.)', '.', author)
if with_dots_no_spaces != author:
    variations.add(with_dots_no_spaces)
    print(f"4. Con puntos sin espacios: {with_dots_no_spaces}")

# 5. Sin tildes (para este caso no cambia)
without_accents = author  # No tiene tildes
variations.add(without_accents)
print(f"5. Sin tildes: {without_accents}")

# 6. Sin puntos ni tildes
without_dots_or_accents = author.replace('.', '')
variations.add(without_dots_or_accents)
print(f"6. Sin puntos ni tildes: {without_dots_or_accents}")

# 7. Completamente normalizado
fully_normalized = re.sub(r'\s+', ' ', without_dots_or_accents).strip()
variations.add(fully_normalized)
print(f"7. Completamente normalizado: {fully_normalized}")

print(f"\nTotal variaciones únicas: {len(variations)}")
print("\nLista completa:")
for i, var in enumerate(sorted(variations), 1):
    print(f"{i}. {var}")
