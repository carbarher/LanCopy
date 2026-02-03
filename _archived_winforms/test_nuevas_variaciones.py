import re


def test_new_variations():
    author = "A. A. Pepito"
    variations = set()

    # Variaciones existentes
    variations.add(author)  # Original

    if '.' in author:
        without_dots = author.replace('.', '')
        variations.add(without_dots)

        normalized = re.sub(r'\s+', ' ', without_dots).strip()
        variations.add(normalized)

        with_dots_no_spaces = re.sub(r'\.(\s+)(?=[A-Z]\.)', '.', author)
        if with_dots_no_spaces != author:
            variations.add(with_dots_no_spaces)

    # NUEVAS VARIACIONES agregadas
    if '.' in author:
        # Solo iniciales con puntos
        initials_only = re.match(r'(?:[A-Z]\.\s*)+', author).group().strip()
        if initials_only and len(initials_only) > 2:
            variations.add(initials_only)

        # Espacios garantizados después de puntos
        with_spaces_after_dots = re.sub(r'\.([A-Z])', r'. \1', author)
        if with_spaces_after_dots != author:
            variations.add(with_spaces_after_dots)

    print(f"Autor: {author}")
    print(f"Total variaciones: {len(variations)}")
    print("\nVariaciones generadas:")
    for i, var in enumerate(sorted(variations), 1):
        print(f"{i}. {var}")

    # Verificar si tenemos las 2 variaciones adicionales
    expected_new = ["A.A.", "A. A. Pepito"]  # Puede que ya exista
    found_new = [v for v in expected_new if v in variations]
    print(f"\n✅ Nuevas variaciones encontradas: {len(found_new)}")
    for var in found_new:
        print(f"   - {var}")


test_new_variations()
