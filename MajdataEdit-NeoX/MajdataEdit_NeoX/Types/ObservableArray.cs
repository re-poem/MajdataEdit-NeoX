using System;
using System.Collections.Generic;

namespace MajdataEdit_NeoX.Types;

public sealed class ObservableArray<T>
{
    private readonly T[] _items;
    private readonly Action<int> _onChanged; // 参数是变化的 index，方便以后细粒度用

    public ObservableArray(int length, Action<int> onChanged)
    {
        _items = new T[length];
        _onChanged = onChanged;
    }

    public int Length => _items.Length;

    public T this[int index]
    {
        get => _items[index];
        set
        {
            if (EqualityComparer<T>.Default.Equals(_items[index], value)) return;
            _items[index] = value;
            _onChanged(index);
        }
    }
}