#!/usr/bin/env python3
# -*- coding: utf-8 -*-

import re


def test_variations():
    """Test de variaciones de nombres de autores"""
    author = "A. A. Pepito"
    variations = set()

    print(f"Test de variaciones para: {author}")
    print("=" * 50)

    # 1. Original
    variations.add(author)
    print(f"1. Original: {author}")

    # 2. Sin puntos
    if '.' in author:
        without_dots = author.replace('.', '')
        variations.add(without_dots)
        print(f"2. Sin puntos: {without_dots}")

        # 3. Sin puntos, espacios normalizados
        normalized = re.sub(r'\s+', ' ', without_dots).strip()
        variations.add(normalized)
        print(f"3. Normalizado: {normalized}")

        # 4. Con puntos, sin espacios entre iniciales
        with_dots_no_spaces = re.sub(r'\.(\s+)(?=[A-Z]\.)', '.', author)
        if with_dots_no_spaces != author:
            variations.add(with_dots_no_spaces)
            print(f"4. Con puntos sin espacios: {with_dots_no_spaces}")

        # 5. SOLO INICIALES CON PUNTOS (NUEVO)
        initials_match = re.match(r'(?:[A-Z]\.\s*)+', author)
        if initials_match:
            initials_only = initials_match.group().strip()
            if len(initials_only) > 2:
                variations.add(initials_only)
                print(f"5. Solo iniciales: {initials_only}")

        # 6. ESPACIOS GARANTIZADOS (NUEVO)
        with_spaces = re.sub(r'\.([A-Z])', r'. \1', author)
        if with_spaces != author:
            variations.add(with_spaces)
            print(f"6. Con espacios garantizados: {with_spaces}")

    # 7. Sin tildes (no aplica a este ejemplo)
    without_accents = author  # No tiene tildes
    variations.add(without_accents)
    print(f"7. Sin tildes: {without_accents}")

    print("=" * 50)
    print(f"TOTAL VARIACIONES ÚNICAS: {len(variations)}")
    print("\nLista completa:")
    for i, var in enumerate(sorted(variations), 1):
        print(f"{i:2d}. {var}")


if __name__ == "__main__":
    test_variations()
