using System;
using System.Collections.Generic;

/// <summary>
/// A value the UI can watch. View models expose these; views Bind to them and redraw when the value changes.
/// This is the whole "binding framework" of the UI — no reflection, no strings.
/// </summary>
public class Observable<T>
{
    private T value;

    public event Action<T> Changed;

    public Observable(T initial = default)
    {
        value = initial;
    }

    public T Value
    {
        get => value;
        set
        {
            if (EqualityComparer<T>.Default.Equals(this.value, value)) return;
            this.value = value;
            Changed?.Invoke(value);
        }
    }

    /// <summary>Listens for changes and is called once right away with the current value.</summary>
    public void Bind(Action<T> listener)
    {
        Changed += listener;
        listener(value);
    }

    public void Unbind(Action<T> listener) => Changed -= listener;
}
