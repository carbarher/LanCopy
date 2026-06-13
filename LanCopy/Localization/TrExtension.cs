using System;
using Avalonia;
using Avalonia.Data;
using Avalonia.Markup.Xaml;

namespace LanCopy.Localization;

/// <summary>
/// Markup extension para XAML: {l:Tr clave}. Devuelve un binding alimentado por
/// un IObservable que emite la traduccion actual y vuelve a emitir cada vez que
/// cambia el idioma (evento Loc.LanguageChanged). Refresca el texto en vivo de
/// forma fiable, sin depender de la notificacion del indexador.
/// </summary>
public sealed class TrExtension : MarkupExtension
{
    public TrExtension() { }
    public TrExtension(string key) { Key = key; }

    public string Key { get; set; } = "";

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return new TrObservable(Key).ToBinding();
    }
}

internal sealed class TrObservable : IObservable<object?>
{
    private readonly string _key;
    public TrObservable(string key) { _key = key; }

    public IDisposable Subscribe(IObserver<object?> observer)
    {
        void Push() => observer.OnNext(Loc.Instance[_key]);
        Push();
        Action handler = Push;
        Loc.Instance.LanguageChanged += handler;
        return new Unsubscriber(() => Loc.Instance.LanguageChanged -= handler);
    }

    private sealed class Unsubscriber : IDisposable
    {
        private Action? _dispose;
        public Unsubscriber(Action dispose) { _dispose = dispose; }
        public void Dispose() { _dispose?.Invoke(); _dispose = null; }
    }
}