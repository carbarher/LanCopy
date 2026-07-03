using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace LanCopy.Models;

// Colección que dispara un solo evento Reset en vez de N Add (#22)
public sealed class BulkObservableCollection<T> : ObservableCollection<T>
{
    public void ReplaceAll(IEnumerable<T> items)
    {
        Items.Clear();

        // M14: pre-asignar capacidad cuando se conoce el tamaño — evita rehashes si la nueva lista
        // es más grande que la capacidad actual de la List<T> interna de ObservableCollection<T>.
        // Items es IList<T> (interfaz), pero ObservableCollection<T> garantiza internamente un List<T>.
        if (items is ICollection<T> col)
        {
            if (Items is List<T> lst && lst.Capacity < col.Count)
                lst.Capacity = col.Count;
            foreach (var item in col) Items.Add(item);
        }
        else
        {
            foreach (var item in items) Items.Add(item);
        }

        OnPropertyChanged(new PropertyChangedEventArgs("Count"));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
