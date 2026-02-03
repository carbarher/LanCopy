import re
import unicodedata

def remove_accents(text):
    nfd = unicodedata.normalize('NFD', text)
    return ''.join(char for char in nfd if unicodedata.category(char) != 'Mn')

def generate_variations(author):
    variations = set()
    variations.add(author)
    print(f'  1. Original: {author}')
    
    if '.' in author:
        without_dots = author.replace('.', '')
        if without_dots.strip():
            variations.add(without_dots)
            print(f'  2. Sin puntos: {without_dots}')
            
            normalized = re.sub(r'\s+', ' ', without_dots).strip()
            if normalized:
                variations.add(normalized)
                print(f'  3. Sin puntos, espacios normalizados: {normalized}')
        
        with_dots_no_spaces = re.sub(r'\.(\s+)(?=[A-Z]\.)', '.', author)
        if with_dots_no_spaces != author and with_dots_no_spaces.strip():
            variations.add(with_dots_no_spaces)
            print(f'  4. Con puntos, sin espacios: {with_dots_no_spaces}')
    
    without_accents = remove_accents(author)
    if without_accents != author:
        variations.add(without_accents)
        print(f'  5. Sin tildes: {without_accents}')
    
    if '.' in author:
        without_dots_or_accents = remove_accents(author.replace('.', ''))
        if without_dots_or_accents.strip():
            variations.add(without_dots_or_accents)
            print(f'  6. Sin puntos ni tildes: {without_dots_or_accents}')
            
            fully_normalized = re.sub(r'\s+', ' ', without_dots_or_accents).strip()
            if fully_normalized:
                variations.add(fully_normalized)
                print(f'  7. Completamente normalizado: {fully_normalized}')
    
    return variations

print('TEST DE VARIACIONES')
print('=' * 40)

test_authors = ['A. A. Pepito']

for author in test_authors:
    print(f'Autor: {author}')
    variations = generate_variations(author)
    print(f'Total variaciones: {len(variations)}')
    print()
    print('Variaciones generadas:')
    for i, var in enumerate(sorted(variations), 1):
        print(f'{i}. {var}')
