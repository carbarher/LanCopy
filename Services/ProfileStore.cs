using System.Collections.Generic;
using System.Linq;

namespace LanCopy.Services;

/// <summary>
/// Operaciones puras sobre la lista de perfiles de conexión (buscar, guardar,
/// eliminar por nombre). Sin dependencias de UI (testeable). Los nombres se
/// comparan de forma sensible a mayúsculas para respetar la entrada del usuario.
/// </summary>
internal static class ProfileStore
{
    public static ConnectionProfile? Find(IEnumerable<ConnectionProfile> profiles, string name)
        => profiles.FirstOrDefault(p => p.Name == name);

    /// <summary>
    /// Inserta o reemplaza un perfil por nombre (un nombre = un perfil). Muta la lista.
    /// </summary>
    public static void Upsert(List<ConnectionProfile> profiles, ConnectionProfile profile)
    {
        profiles.RemoveAll(p => p.Name == profile.Name);
        profiles.Add(profile);
    }

    /// <summary>Elimina el perfil con ese nombre. Devuelve true si eliminó alguno.</summary>
    public static bool Remove(List<ConnectionProfile> profiles, string name)
        => profiles.RemoveAll(p => p.Name == name) > 0;

    public static IReadOnlyList<string> Names(IEnumerable<ConnectionProfile> profiles)
        => profiles.Select(p => p.Name).ToList();
}